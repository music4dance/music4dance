function formatRange(min,max)
{
    if (min == max) {
        return min;
    }
    else {
        return min + '-' + max;
    }
}

// DanceType
var DanceType = function (data) {
    $.extend(true, this, data);

    this.tempoMPM = ko.computed(function () {
        return formatRange(this.TempoRange.Min,this.TempoRange.Max);
    }, this);

    this.tempoBPM = ko.computed(function () {
        var numerator = this.Meter.Numerator;
        return formatRange(this.TempoRange.Min * numerator , this.TempoRange.Max * numerator);
    }, this);
}

var danceMapping = {
    'dances': {
        create: function (options) {
            return new DanceType(options.data);
        }
    }
}

// TODO: Sorting by name is only working in ascending order, not
//  sure what is going on here because sorting everything else works
//  fine - is there some default ko behavior that is overriding us?
function sortString(a, b) {
    return (a < b ? -1 : a > b ? 1 : a == b ? 0 : 0);
}

var viewModel = null;
function setupDances(data) {
    var dances = { 'dances': data };
    viewModel = ko.mapping.fromJS(dances, danceMapping);

    viewModel.headers = [
        { title: 'Name', sortKey: 'Name' },
        { title: 'Meter', sortKey: 'Meter' },
        { title: 'MPM', sortKey: 'MPM' },
        { title: 'BPM', sortKey: 'BPM' }
    ];

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
        }
        var prop = header.sortKey;
        viewModel.dances.sort(function(a,b) {
            return a[prop] < b[prop] ? -1 : a[prop] > b[prop] ? 1 : a[prop] == b[prop] ? 0 : 0;
        });
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