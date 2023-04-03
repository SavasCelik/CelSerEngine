﻿using CelSerEngine.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using CelSerEngine.Native;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using CelSerEngine.Models;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace CelSerEngine.ViewModels;


public class PointerScanResult : ProcessMemory
{
    public List<IntPtr> Offsets { get; set; } = new List<IntPtr>();
    public IntPtr PointingTo { get; set; }

    public PointerScanResult Clone()
    {
        var clone = (PointerScanResult) MemberwiseClone();
        clone.Offsets = clone.Offsets.ToList();

        return clone;
    }

    public override bool Equals(object? obj)
    {
        if (obj is PointerScanResult ps && ps.Address == Address && ps.PointingTo == PointingTo)
        {
            return true;
        }

        return false;
    }

    public override int GetHashCode()
    {
        return Address.GetHashCode() ^ PointingTo.GetHashCode();
    }
}

public partial class PointerScanOptionsViewModel : ObservableRecipient
{
    [ObservableProperty]
    private string pointerScanAddress;
    private readonly SelectProcessViewModel _selectProcessViewModel;
    private readonly int _pointerSize;

    public PointerScanOptionsViewModel(SelectProcessViewModel selectProcessViewModel)
    {
        pointerScanAddress = "";
        _selectProcessViewModel = selectProcessViewModel;
        _pointerSize = sizeof(long);
    }



    private int FindFirstIndexGreaterThanOrEqualToAddress(IReadOnlyList<PointerScanResult> list, long address)
    {
        var startIndex = 0;
        var endIndex = list.Count - 1;
        while (startIndex <= endIndex)
        {
            var midIndex = (startIndex + endIndex) / 2;
            var midAddress = list[midIndex].Address.ToInt64();
            if (midAddress < address)
            {
                startIndex = midIndex + 1;
            }
            else if (midAddress > address)
            {
                endIndex = midIndex - 1;
            }
            else
            {
                return midIndex;
            }
        }
        return startIndex;
    }

    private int FindLastIndexLessThanOrEqualToAddress(IReadOnlyList<PointerScanResult> list, long address)
    {
        var startIndex = 0;
        var endIndex = list.Count - 1;
        while (startIndex <= endIndex)
        {
            var midIndex = (startIndex + endIndex) / 2;
            var midAddress = list[midIndex].Address.ToInt64();
            if (midAddress < address)
            {
                startIndex = midIndex + 1;
            }
            else if (midAddress > address)
            {
                endIndex = midIndex - 1;
            }
            else
            {
                return midIndex;
            }
        }
        return endIndex;
    }

    [RelayCommand]
    public void StartPointerScan()
    {
        var selectedProcess = _selectProcessViewModel.SelectedProcess!;
        var maxSize = 24000;
        var searchedAddress = long.Parse(PointerScanAddress, NumberStyles.HexNumber);
        var staticPointers = GetStaticPointers(selectedProcess);
        var staticPointersByAddress = staticPointers.ToDictionary(x => x.Address);
        var heapPointers = GetHeapPointers(selectedProcess);
        var heapPointersByPointingTo = heapPointers
            .OrderBy(x => x.PointingTo)
            .GroupBy(x => x.PointingTo)
            .ToDictionary(x => x.Key, x => x.ToArray());
        var pointsWhereIWant = heapPointers.Where(x => x.Address.ToInt64() >= searchedAddress - maxSize && x.Address.ToInt64() <= searchedAddress).ToArray();
        var pointerScan1 = new List<PointerScanResult>();
        var searchingPointers = new HashSet<PointerScanResult>();

        foreach (var pointer in pointsWhereIWant)
        {
            var firstOffset = (IntPtr)(searchedAddress - pointer.Address.ToInt64());
            
            pointer.Offsets.Add(firstOffset);
            pointer.PointingTo = (IntPtr)searchedAddress;

            if (heapPointersByPointingTo.TryGetValue(pointer.Address, out var pointers))
            {
                var clonedPointers = pointers.Select(x => x.Clone()).ToList();
                clonedPointers.ForEach(p => p.Offsets.Clear());
                clonedPointers.ForEach(p => p.Offsets.Add(firstOffset));
                if (firstOffset == (IntPtr)0x18 || true)
                {
                    pointerScan1.AddRange(clonedPointers);
                    foreach (var item in clonedPointers)
                    {
                        searchingPointers.Add(item);
                    }
                }
            }
        }

        var sw = Stopwatch.StartNew();

        var counter = new Dictionary<IntPtr, int>();
        for (var currentLevel = 0; currentLevel < 4 - 1; currentLevel++)
        {
            var tempSearchingPointers = searchingPointers.ToArray();
            searchingPointers.Clear();
            foreach (var searchedPointer in tempSearchingPointers)
            {
                var startIndex = FindFirstIndexGreaterThanOrEqualToAddress(heapPointers, searchedPointer.Address.ToInt64() - maxSize);
                var endIndex = FindLastIndexLessThanOrEqualToAddress(heapPointers, searchedPointer.Address.ToInt64());
                for (var i = startIndex; i <= endIndex; i++)
                {
                    var pointer = heapPointers[i];
                    if (heapPointersByPointingTo.TryGetValue(pointer.Address, out var pointers))
                    {
                        var offsets = searchedPointer.Offsets.ToList();
                        var firstOffset = (IntPtr)(searchedPointer.Address.ToInt64() - pointer.Address.ToInt64());
                        offsets.Add(firstOffset);
                        var clonedPointers = pointers.Select(x => x.Clone()).ToList();
                        clonedPointers.ForEach(p => p.Offsets.AddRange(offsets));
                        foreach (var item in clonedPointers)
                        {
                            searchingPointers.Add(item);
                        }

                        var countingFound = counter.TryGetValue(pointer.Address, out int count);
                        if (!staticPointersByAddress.ContainsKey(clonedPointers.First().Address) && countingFound && count >= 3)
                        {
                            heapPointersByPointingTo.Remove(pointer.Address);
                        }
                        else
                        {
                            count++;
                            if (!countingFound)
                            {
                                counter.Add(pointer.Address, count);
                            }
                            counter[pointer.Address] = count;
                        }
                    }
                }
            }
        }

        var pointingThere2 = new List<PointerScanResult>();
        foreach (var pointer in searchingPointers)
        {
            if (staticPointersByAddress.TryGetValue(pointer.Address, out var startingPoint))
            {
                var newPointingThere = new PointerScanResult
                {
                    BaseAddress = startingPoint.BaseAddress,
                    BaseOffset = startingPoint.BaseOffset,
                    Offsets = pointer.Offsets,
                    PointingTo = (IntPtr)searchedAddress
                };
                pointingThere2.Add(newPointingThere);
            }
        }

        //var pointingThere = new List<PointerScanResult>();
        //var level = 4;
        //var counter = new Dictionary<IntPtr, int>();

        //for (var currentLevel = 0; currentLevel < level; currentLevel++)
        //{
        //    var pointerList = pointerScan1.OrderBy(x => x.Offsets.Last()).ToArray();
        //    pointerScan1.Clear();
        //    foreach (var pointer in pointerList)
        //    {
        //        if (staticPointersByAddress.TryGetValue(pointer.Address, out var startingPoint))
        //        {
        //            var newPointingThere = new PointerScanResult
        //            {
        //                BaseAddress = startingPoint.BaseAddress,
        //                BaseOffset = startingPoint.BaseOffset,
        //                Offsets = pointer.Offsets,
        //                PointingTo = (IntPtr)searchedAddress
        //            };
        //            pointingThere.Add(newPointingThere);
        //        }

        //        if (currentLevel == level - 1)
        //        {
        //            continue;
        //        }

        //        var newAddy = IntPtr.Zero;
        //        for (int i = 0; i < maxSize; i += 4)
        //        {

        //            newAddy = pointer.Address - i;
        //            if (heapPointersByPointingTo.TryGetValue(newAddy, out var pointers))
        //            {
        //                var clonedPointers = pointers.Select(x => x.Clone()).ToList();
        //                var offsets = new List<IntPtr>(pointer.Offsets);
        //                offsets.Add((IntPtr)i);
        //                clonedPointers.ForEach(x => x.Offsets = new List<IntPtr>(offsets));
        //                pointerScan1.AddRange(clonedPointers);
        //                var countingFound = counter.TryGetValue(newAddy, out int count);
        //                if (!staticPointersByAddress.ContainsKey(clonedPointers.First().Address) && countingFound && count >= 3)
        //                {
        //                    heapPointersByPointingTo.Remove(newAddy);
        //                }
        //                else
        //                {
        //                    count++;
        //                    if (!countingFound)
        //                    {
        //                        counter.Add(newAddy, count);
        //                    }
        //                    counter[newAddy] = count;
        //                }
        //            }
        //        }
        //    }
        //}

        sw.Stop();
        
        var resultsts = pointingThere2.OrderBy(x => x.Offsets.Count).ToArray();

        //-- rescan --//
        var nextAddy = IntPtr.Zero;
        staticPointers = GetStaticPointers(selectedProcess);
        staticPointersByAddress = staticPointers.ToDictionary(x => x.Address);
        heapPointers = GetHeapPointers(selectedProcess);
        var heapPointerByAddress = heapPointers.GroupBy(x => x.Address).ToDictionary(x => x.Key, x => x.First());
        var results0 = resultsts.Where(x => x.BaseOffset == 0x325b00 && x.Offsets.Count == 4).ToArray();

        var foundPointer = new List<PointerScanResult>();
        foreach (var resultItem in resultsts)
        {
            if (staticPointersByAddress.TryGetValue(resultItem.Address, out var pointsTo))
            {
                for (int i = resultItem.Offsets.Count - 1; i >= 0; i--)
                {
                    var offset = resultItem.Offsets[i];
                    var addressWithOffset = pointsTo.PointingTo + offset.ToInt32();
                    if (addressWithOffset == nextAddy)
                    {
                        foundPointer.Add(resultItem);
                    }
                    else if (heapPointerByAddress.TryGetValue(addressWithOffset, out var pointsToWithOffset))
                    {
                        pointsTo = pointsToWithOffset;
                    }
                }
            }
        }

        var resultsts2 = foundPointer.ToArray();

    }

    public bool ShowPointerScanDialog(string pointerScanAddress = "")
    {
        PointerScanAddress = pointerScanAddress;
        var pointerScanOpstionsDlg = new PointerScanOptions()
        {
            Owner = App.Current.MainWindow
        };


        return pointerScanOpstionsDlg.ShowDialog() ?? false;
    }

    private IReadOnlyList<PointerScanResult> GetStaticPointers(ProcessAdapter process)
    {
        var baseAddress = process.MainModule!.BaseAddress;
        var regionSize = process.MainModule!.ModuleMemorySize;
        var currentSize = 0;
        var listOfBaseAddresses = new List<PointerScanResult>();

        while (currentSize < regionSize)
        {
            var buffer = NativeApi.ReadVirtualMemory(process.GetProcessHandle(), (IntPtr)baseAddress + currentSize, (uint)_pointerSize);
            var foundAddress = BitConverter.ToInt64(buffer);
            listOfBaseAddresses.Add(new PointerScanResult
            {
                BaseAddress = baseAddress,
                BaseOffset = currentSize,
                Memory = buffer,
                PointingTo = (IntPtr)foundAddress
            });

            currentSize += _pointerSize;
        }

        return listOfBaseAddresses;
    }

    private IReadOnlyList<PointerScanResult> GetHeapPointers(ProcessAdapter process)
    {
        var pages = NativeApi.GatherVirtualPages(process.GetProcessHandle());
        var allAddresses = new List<PointerScanResult>();

        foreach (var page in pages)
        {
            var pageSpan = page.Bytes.AsSpan();
            for (int i = 0; i < (int)page.RegionSize; i += _pointerSize)
            {
                if (i + _pointerSize > (int)page.RegionSize)
                {
                    continue;
                }
                var bufferValue = BitConverter.ToInt64(page.Bytes, i);
                var entry = new PointerScanResult
                {
                    Memory = pageSpan.Slice(i, _pointerSize).ToArray(),
                    BaseAddress = new IntPtr((long)page.BaseAddress),
                    BaseOffset = i,
                    PointingTo = (IntPtr)bufferValue
                };
                allAddresses.Add(entry);
            }
        }

        return allAddresses
            .Where(x => x.PointingTo != IntPtr.Zero && x.Address.ToInt64() % 4 == 0)
            //.Where(x => x.PointingTo.ToInt64() >= 0x10000 && x.PointingTo.ToInt64() <= 0x7ffffffeffff)
            .OrderBy(x => x.Address)
            .ToArray();
    }
}
