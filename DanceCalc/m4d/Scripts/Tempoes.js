// DanceType
var DanceType = function (data,parent) {
    $.extend(true, this, data);

    this.tempoMPM = ko.computed(function () {
        var tempo = this.computedTempo();
        return this.formatRange(tempo.Min,tempo.Max);
    }, this);

    this.tempoBPM = ko.computed(function () {
        var tempo = this.computedTempo();
        var numerator = this.Meter.Numerator;
        return this.formatRange(tempo.Min * numerator , tempo.Max * numerator);
    }, this);

    this.computedTempo = ko.computed(function () {
        var range = this.TempoRange;
        var si = parent.styleFilter();
        if (si !== 0)
        {
            for (var i = 0; i < this.Instances.length; i++)
            {
                if (danceStyles[si].name === this.Instances[i].Style)
                {
                    range = this.Instances[i].TempoRange;
                }
            }
        }
        return range;
    },this);

    this.styles = ko.computed(function () {
        var ret = "";
        if (this.Instances)
        {
            var sep = "";
            for (var i = 0 ; i < this.Instances.length; i++)
            {
                ret += sep;
                ret += this.Instances[i].Style;
                sep = " , "
            }
        }

        return ret;
    }, this);

    this.checkFilter = function () {
        var m = parent.meterFilter() === 1 || parent.meterFilter() === this.Meter.Numerator;
        var ti = parent.typeFilter(); 
        var t = ti === 0 || danceTypes[ti].name === this.GroupName;
        var si = parent.styleFilter();
        var s = true;
        if (si !== 0)
        {
            s = false;
            for (var i = 0; i < this.Instances.length; i++)
            {
                if (danceStyles[si].name === this.Instances[i].Style)
                {
                    s = true;
                    break;
                }
            }
        }
        return m && t && s;
    };

    this.formatRange = function(min, max) {
        if (min == max) {
            return min;
        }
        else {
            return min + '-' + max;
        }
    }
}

var danceMapping = {
    'dances': {
        create: function (options) {
            return new DanceType(options.data,options.parent);
        }
    }
}

var rootMapping = {
    create: function (options) {
        var self = ko.mapping.fromJS(options.data,danceMapping);

        self.headers = [
            { title: 'Name', sortKey: 'Name' },
            { title: 'Meter', sortKey: 'Meter' },
            { title: 'MPM', sortKey: 'MPM' },
            { title: 'BPM', sortKey: 'BPM' },
            { title: 'Type', sortKey: 'Type' },
            { title: 'Style(s)', sortKey: 'Style' },
        ];

        self.styleFilter = ko.observable(0);
        self.typeFilter = ko.observable(0);
        self.meterFilter = ko.observable(1);

        self.testObs = ko.observable('testing');
        self.testStr = 'testing';

        return self;
    }
}

function sortString(a, b) {
    return (a < b ? -1 : a > b ? 1 : a == b ? 0 : 0);
}

function setupDances(data) {
    var dances = { 'dances': data };
    var viewModel = ko.mapping.fromJS(dances, rootMapping);

    viewModel.activeSort = null;

    viewModel.sort = function(header, event)
    {
        //if this header was just clicked a second time...
        if (self.activeSort === header) {
            header.desc = !header.desc; //...toggle the direction of the sort
        } else {
            self.activeSort = header; //first click, remember it
        }

        var dir = header.desc ? -1 : 1;

        var sortKey = header.sortKey;

        switch (sortKey)
        {
            case 'Name': 
                viewModel.dances.sort(function (a, b) {
                    if (header.desc)
                        return sortString(b.Name, a.Name);
                    else
                        return sortString(a.Name, b.Name);
                });
                break;
            case 'Meter':
                viewModel.dances.sort(function (a, b) {
                    return (a.Meter.Numerator - b.Meter.Numerator) * dir;
                });
                break;
            case 'MPM':
                viewModel.dances.sort(function (a, b) {
                    return (a.TempoRange.Min - b.TempoRange.Max) * dir;
                });
                break;
            case 'BPM':
                viewModel.dances.sort(function (a, b) {
                    return ((a.TempoRange.Min * a.Meter.Numerator) - (b.TempoRange.Max * b.Meter.Numerator)) * dir;
                });
                break;
            case 'Type':
                viewModel.dances.sort(function (a, b) {
                    return sortString(a.GroupName,b.GroupName) * dir;
                });
                break;
            case 'Style':
                viewModel.dances.sort(function (a, b) {
                    return sortString(a.styles(), b.styles()) * dir;
                });
                break;
        }
        var prop = header.sortKey;
    }

    viewModel.dances.sort(function (a, b) { return sortString(a.Name, b.Name) });

    viewModel.filteredDances = ko.computed(function () {
        if (viewModel.meterFilter() === 1 && viewModel.typeFilter() === 0 && viewModel.styleFilter() === 0)
        {
            return viewModel.dances();
        }
        else
        {
            return ko.utils.arrayFilter(viewModel.dances(), function (item) {
                return item.checkFilter();
            });
        }
    })

    viewModel.handleButton = function (evt, filter) {
        evt.preventDefault();
        var id = evt.target.id;
        var idx = id.lastIndexOf("-");
        var base = id.substring(0, idx);
        $("#" + base).html(evt.target.innerText + " <span class='caret'></span>");
        filter(evt.data);
    }


    for (var i = 1; i <= 4; i++) {
        var id = "#filter-meter-" + i;
        $(id).click(i, function (evt) { viewModel.handleButton(evt, viewModel.meterFilter) });
    }

    for (var i = 0; i < danceTypes.length; i++) {
        var id = "#filter-type-" + danceTypes[i].id;
        $(id).click(i, function (evt) { viewModel.handleButton(evt, viewModel.typeFilter) });
    }

    for (var i = 0; i < danceStyles.length; i++) {
        var id = "#filter-style-" + danceStyles[i].id;
        $(id).click(i, function (evt) { viewModel.handleButton(evt, viewModel.styleFilter) });
    }

    ko.applyBindings(viewModel);
}


$(document).ready(function () {
    var uri = '/api/dance?details=true';
    $.getJSON(uri)
        .done(function (data) {
            setupDances(data);
        })
        .fail(function (jqXHR, textStatus, err) {
            window.alert(err);
        });
});