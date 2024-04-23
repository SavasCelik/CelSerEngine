﻿using CelSerEngine.Core.Comparators;
using CelSerEngine.Core.Models;
using CelSerEngine.Core.Native;
using CelSerEngine.WpfBlazor.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using System.Diagnostics;

namespace CelSerEngine.WpfBlazor.Components;

public partial class Index : ComponentBase, IAsyncDisposable
{
    [Inject]
    private INativeApi NativeApi { get; set; } = default!;

    [Inject]
    private EngineSession EngineSession { get; set; } = default!;

    [Inject]
    private MainWindow MainWindow { get; set; } = default!;

    private VirtualizedAgGrid<ScanResultItem> _virtualizedAgGridRef = default!;
    private List<ScanResultItem> _scanResultItems { get; set; } = [];

    private string _searchValue { get; set; } = string.Empty;
    private ScanDataType _selectedScanDataType { get; set; } = ScanDataType.Integer;
    private ScanCompareType _selectedScanCompareType { get; set; } = ScanCompareType.ExactValue;
    private float _progressBarValue { get; set; }
    private bool _isFirstScan { get; set; } = true;
    private readonly Timer _scanResultsUpdater;
    private readonly IProgress<float> _progressBarUpdater;

    public Index()
    {
        _scanResultsUpdater = new Timer((e) => UpdateVisibleScanResults(), null, Timeout.Infinite, 0);
        _progressBarUpdater = new Progress<float>(newValue =>
        {
            if (newValue - _progressBarValue >= 10)
            {
                _progressBarValue = newValue;
                StateHasChanged();
            }
        });
    }

    protected override void OnInitialized()
    {
        var process = Process.GetProcessesByName("SmallGame").First();
        var selectedProcess = new ProcessAdapter(process)
        {
            ProcessHandle = NativeApi.OpenProcess(process.Id)
        };

        EngineSession.SelectedProcess = selectedProcess;
        EngineSession.OnChange += StateHasChanged;
    }

    private async Task FirstScan()
    {
        _scanResultItems.Clear();
        await _virtualizedAgGridRef.ApplyDataAsync();
        await _virtualizedAgGridRef.ShowScanningOverlay();
        var results = await Task.Run(() =>
        {
            var virtualMemoryRegions = NativeApi.GatherVirtualMemoryRegions(EngineSession.SelectedProcessHandle);
            var scanConstraint = new ScanConstraint(_selectedScanCompareType, _selectedScanDataType, _searchValue);
            var comparer = ComparerFactory.CreateVectorComparer(scanConstraint);

            return comparer.GetMatchingMemorySegments(virtualMemoryRegions, _progressBarUpdater);
        });

        _progressBarValue = 100;
        StateHasChanged();
         _scanResultItems.AddRange(results.Select(x => new ScanResultItem(x)));
        await _virtualizedAgGridRef.ApplyDataAsync();
        _scanResultsUpdater.Change(TimeSpan.Zero, TimeSpan.FromSeconds(1));
        _progressBarValue = 0;
        _isFirstScan = false;
    }

    public async Task Enter(KeyboardEventArgs e)
    {
        if (e.Code == "Enter")
        {
            await FirstScan();
        }
    }

    private void OpenSelectProcess()
    {
        MainWindow.OpenProcessSelector();
    }

    private async Task NextScan()
    {
        await _virtualizedAgGridRef.ShowScanningOverlay();
        var scanConstraint = new ScanConstraint(_selectedScanCompareType, _selectedScanDataType, _searchValue);
        NativeApi.UpdateAddresses(EngineSession.SelectedProcessHandle, _scanResultItems);
        var passedMemorySegments = new List<ScanResultItem>();

        for (var i = 0; i < _scanResultItems.Count; i++)
        {
            if (ValueComparer.MeetsTheScanConstraint(_scanResultItems[i].Value, scanConstraint.UserInput, scanConstraint))
            {
                _scanResultItems[i].PreviousValue = _scanResultItems[i].Value;
                passedMemorySegments.Add(_scanResultItems[i]);
            }
        }

        _scanResultItems.Clear();
        _scanResultItems.AddRange(passedMemorySegments);
        await _virtualizedAgGridRef.ApplyDataAsync();
    }

    private async void UpdateVisibleScanResults()
    {
        var visibleItems = _virtualizedAgGridRef.GetVisibleItems().ToList();

        if (visibleItems.Count == 0)
            return;

        NativeApi.UpdateAddresses(EngineSession.SelectedProcessHandle, visibleItems);

        if (!_virtualizedAgGridRef.IsDisposed)
            await _virtualizedAgGridRef.ApplyDataAsync();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        EngineSession.OnChange -= StateHasChanged;
        await _scanResultsUpdater.DisposeAsync();
    }
}
