// DanceHeader
var DanceHeader = function(title,key,detail,parent) {
    var self = this;
    this.title = title;
    this.sortKey = key;
    this.detailed = detail;

    this.show = ko.computed(function () {
        return !self.detailed || self.detailed();
    }, this);
}

// DanceType
var DanceType = function (data, parent) {
    var self = this;
    $.extend(true, this, data);

    this.SeoName = this.Name.toLowerCase().replace(' ', '-');

    this.tempoMPM = ko.computed(function () {
        return self.tempoHelper(parent.styleFilter(), parent.orgFilter(), 1);
    }, this);

    this.tempoBPM = ko.computed(function () {
        return self.tempoHelper(parent.styleFilter(), parent.orgFilter(), this.Meter.Numerator);
    }, this);

    this.tempoDS = ko.computed(function () {
        return self.tempoHelper(parent.styleFilter(), 1, 1);
    }, this);

    this.tempoNDCA = ko.computed(function () {
        var ret = self.tempoHelper(parent.styleFilter(), 3, 1);
        if (!ret) ret = self.tempoHelper(parent.styleFilter(), 5, 1);
        return ret;
    }, this);

    this.tempoNDCABeginner = ko.computed(function () {
        var ret = self.tempoHelper(parent.styleFilter(), 4, 1);
        if (!ret) ret = self.tempoHelper(parent.styleFilter(), 6, 1);
        return ret;
    }, this);

    this.computedTempi = ko.computed(function () {
        return self.computeTempi(parent.styleFilter(), 0);
    }, this);

    this.styles = ko.computed(function () {
        var ret = '';
        if (this.Instances)
        {
            var sep = '';
            for (var i = 0 ; i < this.Instances.length; i++) {
                var style = this.Instances[i].Style;
                var ps = window.danceStyles[parent.styleFilter()].name;

                if (parent.styleFilter() === 0 || style === ps) {
                    ret += sep;
                    ret += style;

                    if (parent.showDetails()) {
                        sep = '<br>';
                    }
                    else {
                        sep = ', ';
                    }
                }
            }
        }
        return ret;
    }, this);

    this.checkFilter = function () {
        var m = parent.meterFilter() === 1 || parent.meterFilter() === this.Meter.Numerator;
        var ti = parent.typeFilter(); 
        var t = ti === 0 || window.danceTypes[ti].name === this.GroupName;
        var si = parent.styleFilter();
        var s = true;
        if (si !== 0)
        {
            s = false;
            for (var i = 0; i < this.Instances.length; i++)
            {
                if (window.danceStyles[si].name === this.Instances[i].Style)
                {
                    s = true;
                    break;
                }
            }
        }

        var o = true;
        var oi = parent.orgFilter();
        if (oi !== 0)
        {
            if (!this.Organizations || this.Organizations.indexOf(danceOrgs[oi].name) == -1)
            {
                o = false;
            }
        }

        return m && t && s && o;
    };

    this.formatRange = function(min, max) {
        if (min === max) {
            return min;
        }
        else {
            return min + '-' + max;
        }
    }

    this.computeTempo = function (si,oi) {
        var range = this.TempoRange;

        if (si || oi) {
            range = null;
            for (var i = 0; i < this.Instances.length; i++) {
                if (!si || (window.danceStyles[si].name === this.Instances[i].Style)) {
                    var instance = this.Instances[i];
                    var match = false;
                    if (oi && instance.Exceptions && instance.Exceptions.length > 0) {
                        var org = window.danceOrgs[oi];
                        for (var j = 0; j < instance.Exceptions.length; j++) {
                            var ex = instance.Exceptions[j];
                            if (this.matchException(ex, org)) {
                                range = this.unionRange(range, ex.TempoRange);
                                match = true;
                            }
                        }
                    }
                    if (!match && (oi === 0 || this.Instances[i].Style !== 'Social')) {
                        range = this.unionRange(range, instance.TempoRange);
                    }
                }
            }
        }
        return range;
    };

    this.computeTempi = function (si, oi) {
        var ret = [];
        var tempo;
        if (si === 0) {
            for (var i = 0 ; i < this.Instances.length; i++) {
                var siT = this.findStyleIndex(this.Instances[i].Style);
                tempo = this.computeTempo(siT, oi);
                if (tempo) ret.push(tempo);
            }
        }
        else {
            tempo = this.computeTempo(si, oi);
            if (tempo) ret.push(tempo);
        }

        return ret;
    };

    this.tempoHelper = function (si, oi, numerator) {
        var ret = '';
        if (parent.showDetails()) {
            var sep = '';
            var tempi = this.computeTempi(si, oi);
            for (var i = 0; i < tempi.length; i++) {
                ret += sep;
                ret += this.formatRange(tempi[i].Min * numerator, tempi[i].Max * numerator);
                sep = '<br>';
            }
        }
        else {
            var tempo = this.computeTempo(si, oi);
            if (tempo) {
                ret = this.formatRange(tempo.Min * numerator, tempo.Max * numerator);
            }
            
        }
        return ret;
    }

    this.findStyleIndex = function (style) {
        for (var i = 0; i < window.danceStyles.length; i++) {
            if (window.danceStyles[i].name === style)
                return i;
        }
        return -1;
    };

    this.matchException = function(ex, org) {
        if (ex.Organization !== org.name) {
            return false;
        }
        else if (ex.Level === 'All' && ex.Competitor === "All") {
            return true;
        }
        else {
            return (org.category === 'Level' && ex.Level === org.qualifier) ||
             (org.category === 'Competitor' && ex.Competitor === org.qualifier);
        }
    }

    this.unionRange = function(a,b) {
        if (!a) return b;
        if (!b) return a;

        return {Min: Math.min(a.Min,b.Min), Max: Math.max(a.Max,b.Max)};
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

        self.showDanceSport = ko.computed(function () {
            return self.showDetails() && (self.orgFilter() === 0);
        }, this);

        self.showNDCA_A = ko.computed(function () {
            return self.showDetails() && (self.orgFilter() === 0 || self.orgFilter() === 2);
        }, this);

        self.showNDCA_B = ko.computed(function () {
            return self.showDetails() && (self.orgFilter() === 0 || self.orgFilter() === 2);
        }, this);

        self.headers = ko.observableArray([
            new DanceHeader('Name', 'Name', null,self),
            new DanceHeader('Meter', 'Meter', null, self),
            new DanceHeader('MPM', 'MPM', null, self),
            new DanceHeader('DanceSport', 'BPM', self.showDanceSport,self),
            new DanceHeader('NDCA A*', 'BPM', self.showNDCA_A, self),
            new DanceHeader('NDCA B*', 'BPM', self.showNDCA_B, self),
            new DanceHeader('BPM', 'BPM', null, self),
            new DanceHeader('Type', 'Type', null, self),
            new DanceHeader('Style(s)', 'Style', null, self)
        ]);

        self.styleFilter = ko.observable(window.paramStyle);
        self.typeFilter = ko.observable(window.paramType);
        self.meterFilter = ko.observable(window.paramMeter);
        self.orgFilter = ko.observable(window.paramOrg);

        self.showDetails = ko.observable(window.paramDetailed);

        return self;
    }
}

function sortString(a, b) {
    return (a < b ? -1 : (a > b ? 1 : 0));
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
                    return sortString(b.Name, a.Name) * dir;
                });
                break;
            case 'Meter':
                viewModel.dances.sort(function (a, b) {
                    return (a.Meter.Numerator - b.Meter.Numerator) * dir;
                });
                break;
            case 'MPM':
                viewModel.dances.sort(function (a, b) {
                    return (a.TempoRange.Min - b.TempoRange.Min) * dir;
                });
                break;
            case 'BPM':
                viewModel.dances.sort(function (a, b) {
                    return ((a.TempoRange.Min * a.Meter.Numerator) - (b.TempoRange.Min * b.Meter.Numerator)) * dir;
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

    viewModel.filteredDances = ko.computed(function() {
        if (viewModel.meterFilter() === 1 && viewModel.typeFilter() === 0 && viewModel.styleFilter() === 0 && viewModel.orgFilter() === 0) {
            return viewModel.dances();
        } else {
            return ko.utils.arrayFilter(viewModel.dances(), function(item) {
                return item.checkFilter();
            });
        }
    });

    viewModel.handleButton = function (evt, filter) {
        evt.preventDefault();
        var idT = evt.target.id;
        var idx = idT.lastIndexOf('-');
        var base = idT.substring(0, idx);
        $('#' + base).html(evt.target.innerText + ' <span class=\'caret\'></span>');
        filter(evt.data);
    }
    var i;
    var id;
    for (i = 1; i <= 4; i++) {
        id = '#filter-meter-' + i;
        $(id).click(i, function (evt) { viewModel.handleButton(evt, viewModel.meterFilter) });
    }

    for (i = 0; i < window.danceTypes.length; i++) {
        id = '#filter-type-' + window.danceTypes[i].id;
        $(id).click(i, function (evt) { viewModel.handleButton(evt, viewModel.typeFilter) });
    }

    for (i = 0; i < window.danceStyles.length; i++) {
        id = '#filter-style-' + window.danceStyles[i].id;
        $(id).click(i, function (evt) { viewModel.handleButton(evt, viewModel.styleFilter) });
    }

    for (i = 0; i < window.danceOrgs.length; i++) {
        id = '#filter-org-' + i;
        $(id).click(i, function (evt) { viewModel.handleButton(evt, viewModel.orgFilter) });
    }

    $('#reset').click(function (evt) {
        evt.preventDefault();
        viewModel.styleFilter(0);
        viewModel.typeFilter(0);
        viewModel.meterFilter(1);
        viewModel.orgFilter(0);
    });

    // Do initial sort
    viewModel.dances.sort(function (a, b) { return sortString(a.Name, b.Name) });

    ko.applyBindings(viewModel);
}


$(document).ready(function () {
    setupDances(dances);
});
