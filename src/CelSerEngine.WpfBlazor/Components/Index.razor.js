let modulesSelect;

export function initIndex() {
    new SlimSelect({
        select: '#selectScanDataType',
        settings: {
            showSearch: false,
        }
    });

    new SlimSelect({
        select: '#writable',
        settings: {
            showSearch: false,
        }
    });

    new SlimSelect({
        select: '#executable',
        settings: {
            showSearch: false,
        }
    });

    new SlimSelect({
        select: '#copyOnWrite',
        settings: {
            showSearch: false,
        }
    });

    new SlimSelect({
        select: '#memoryTypes',
        settings: {
            showSearch: false,
        }
    });

    initModulesSelect();

    Split(['#split-0', '#split-1'], {
        direction: 'vertical',
        snapOffset: 0,
        minSize: [200, 100],
        gutterSize: 1,
        elementStyle: function (dimension, size, gutterSize) {
            return {
                'flex-basis': 'calc(' + size + '% - ' + gutterSize + 'px)',
            }
        },
        gutterStyle: function (dimension, gutterSize) {
            return {
                'flex-basis': gutterSize + 'px',
                'min-height': gutterSize + 'px',
            }
        },
    });
}

export function focusFirstInvalidInput() {
    setTimeout(() => document.querySelector(".invalid")?.focus(), 100);
}

export function focusSearchValueInput() {
    setTimeout(() => document.querySelector("#value-text-field")?.focus(), 100);
}

export function updateModules(data) {
    const modulesSelectEl = document.getElementById("modulesSelect");
    modulesSelectEl.innerHTML = "";

    for (let i = 0; i < data.length; i++) {
        const option = document.createElement("option");
        option.text = data[i];
        option.value = data[i];
        modulesSelectEl.add(option);
    }

    destroyModulesSelect();
    initModulesSelect();
    const event = new Event('change');
    modulesSelectEl.dispatchEvent(event);
}

function destroyModulesSelect() {
    modulesSelect.destroy();
}

function initModulesSelect() {
    modulesSelect = new SlimSelect({
        select: '#modulesSelect',
        settings: {
            //showSearch: false,
        }
    });
}