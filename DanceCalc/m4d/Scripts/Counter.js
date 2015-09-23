var counter = function() {
    var self = this;

// These variables all have optional input variables named "param*" to set up
//  different variations
    self.showBPM = false;
    self.showMPM = true;
    self.numerator = 1;
    self.defVisible = .05;
    self.epsVisible = defVisible;
    self.tempo = 0;

    self.counter = 0;
    self.start = new Date().getTime();

    self.defWait = 5 * 1000;
    self.maxCounts = 50;

    self.intervals = [];

    self.maxWait = defWait;
    self.last = 0;
    self.average = 0;
    self.rate = 0.0;

    self.dances = [];
    self.danceIndex = null;

    self.timeout = null;

    self.labels = ['BPM ', '2/4 MPM ', '3/4 MPM ', '4/4 MPM '];

    self.tempoId = '#Tempo';
    self.mpmId = '#MPM';

    self.danceAction = function (id) {
        var name = null;

        for (var i = 0; i < dances.length; i++) {
            if (dances[i].Id === id) {
                name = dances[i].SeoName;
            }
        }

        if (name)
            window.location.href = '/dances/' + name;
    }

    self.roundTempo = function (t) {
        if ($.isNumeric(t)) {
            var r = Math.round(t * 10) / 10;
            return Number(r.toFixed(1));
        } else {
            return 0.0;
        }
    }

    self.formatTempo = function (range, meter) {
        if (!self.showMPM && !self.showBPM)
            return '';

        var ret = '<small>(';

        if (self.showMPM) {
            ret += range.Min;
            if (range.Min !== range.Max) {
                ret += '-' + range.Max;
            }
            ret += ' MPM ' + meter.Numerator + '/4';

            if (showBPM) {
                ret += ' & ';
            }
        }

        if (showBPM) {
            ret += range.Min * meter.Numerator;
            if (range.Min !== range.Max) {
                ret += '-' + range.Max * meter.Numerator;
            }
            ret += 'BPM ';
        }

        ret += ')<small>';
        return ret;
    }

    self.display = function () {
        $('#total').text(counter);
        var t = new Date().getTime();
        var dt = t - start;
        $('#time').text(dt);
        $('#avg').text(Math.round(average));
        $('#rate').text(rate);

        if (!$(self.mpmId).is(':focus') || self.rate === 0) {
            $(self.mpmId).val(rate);
        }
        if (!$(self.tempoId).is(':focus') || self.rate === 0) {
            $(self.tempoId).val(roundTempo(self.rate * self.numerator));
        }

        var bt = self.last === 0 ? 'Count' : 'Again';
        $('#count').html(bt);
        $('#dances').empty();

        var idpfx = 'add-dance-';
        for (var i = 0; i < self.dances.length; i++) {
            var dance = self.dances[i];
            var text = '<a id=\'' + idpfx + dance.Id + '\' href=\'#\' class=\'list-group-item ';
            // This conversion is necessary to convert the single decimal precision string to a number that can be exactly compared
            var tempoDelta = parseInt(dance.TempoDelta);

            if (tempoDelta === 0) {
                text += ' list-group-item-info\'>';
            } else {
                var type = (tempoDelta < 0) ? 'list-group-item-danger' : 'list-group-item-success';
                text += type + '\'>' +
                    '<span class=\'badge\'>' + dances[i].TempoDelta + 'MPM</span>';
                // The above version of tempoDelta is explicitly the formatted string version
            }
            var strong = self.numerator === 1 || numerator === dance.Meter.Numerator;
            if (strong) text += '<strong>';
            text += dance.Name;
            if (strong) text += '</strong>';
            text += ' ' + self.formatTempo(dance.TempoRange, dance.Meter) + '</div>';

            $('#dances').append(text);

            if (typeof window.danceAction == 'function') {
                $('#add-dance-' + dance.Id).click(function() {
                    var id = $(this).context.id;
                    id = id.substring(idpfx.length);
                    window.danceAction(id);
                });
            }
        }

        $('#help').toggle(dances.length === 0);
    }

    self.timerReset = function (noRefresh) {
        self.start = new Date().getTime();
        self.counter = 0;
        self.average = 0;
        self.intervals = [];
        //rate = 0;
        self.last = 0;
        if (!noRefresh) {
            self.display();
        }
    }

    self.doReset = function () {
        self.dances = [];
        self.rate = average.toFixed(1);
        self.maxWait = self.defWait;
        self.timerReset();
        self.epsVisible = self.defVisible;

        $('#epsilon').val(epsVisible * 100);
    }

    self.getRate = function () {
        var ret = 0;
        if (self.average !== 0)
            ret = 60 * 1000 / self.average;

        return self.roundTempo(ret);
    }

    self.updateDances = function() {
        self.dances = [];

        var bpm = self.rate * self.numerator;

        for (var i = 0; i < self.danceIndex.length; i++) {
            var dance = self.danceIndex[i];

            if (numerator === 1 || dance.Meter.Numerator === numerator ||
                (numerator === 2 && dance.Meter.Numerator === 4) || (numerator === 4 && dance.Meter.Numerator === 2)) {

                var delta = 0;

                var tempRate = bpm / dance.Meter.Numerator;

                if (tempRate < dance.TempoRange.Min) {
                    delta = tempRate - dance.TempoRange.Min;
                } else if (tempRate > dance.TempoRange.Max) {
                    delta = tempRate - dance.TempoRange.Max;
                }

                var avg = (dance.TempoRange.Min + dance.TempoRange.Max) / 2;
                var eps = delta / avg;

                if (Math.abs(eps) < epsVisible) {
                    dance.TempoDelta = delta.toFixed(1);
                    dance.TempoEps = eps;

                    self.dances.push(dance);
                }
            }
        }

        dances.sort(function(a, b) {
            return Math.abs(a.TempoEps) - Math.abs(b.TempoEps);
        });

        self.display();
    }

    self.updateRate = function (newRate) {
        console.log('Rate=' + newRate);
        if (self.rate === newRate) {
            return;
        }

        self.rate = newRate;

        self.updateDances();

        serviceLookup.setTempo(newRate);
    }

    self.doClick = function () {
        if (timeout) {
            window.clearTimeout(timeout);
        }
        var current = new Date().getTime();
        if (self.last === 0) {
            self.last = current;
            self.display();
        } else {
            var delta = current - last;
            self.last = current;

            if (delta >= self.maxWait) {
                self.doReset();
            } else {
                self.intervals.push(delta);
                if (self.intervals.length > self.maxCounts)
                    self.intervals.shift();

                var ms = 0;
                for (var i = 0; i < self.intervals.length; i++) {
                    ms += self.intervals[i];
                }

                var old = self.average;
                self.average = ms / self.intervals.length;
                delta = self.average - old;
                //var dp = delta / average;
                //maxWait = average * 2;

                self.counter += 1;

                if (Math.abs(delta) >= .1) {
                    self.updateRate(self.getRate());
                } else {
                    self.display();
                }
            }
        }

        self.timeout = window.setTimeout(function () { timerReset(); }, maxWait);
    }

    self.rateFromText = function(text) {
        var r = Number(text);
        return roundTempo(r);
    }

    self.setupDances=function(data) {
        self.danceIndex = data;

        var tempoT = rateFromText($(self.tempoId).val());
        var mpmT = rateFromText($(self.mpmId).val());

        if (mpmT !== 0) {
            self.updateRate(mpmT);
        } else if (tempoT !== 0) {
            self.updateRate(roundTempo(tempoT / numerator));
        }
    }

    self.setNumeratorControl=function(num,force) {
        if (force || self.numerator !== num) {
            $('#mt').empty();
            $('#mt').append(labels[num - 1]);
            $('#mt').append('<span class=\'caret\'></span>');
            self.numerator = num;
        }
    }

    self.setNumerator=function(num) {
        if (self.numerator !== num) {
            var old = self.numerator;
            self.setNumeratorControl(num);

            var r = (old * self.rate) / num;
            r = self.roundTempo(r);

            self.timerReset(self.rate !== 0);
            if (self.rate !== 0) {
                self.updateRate(r);
            }
        }
    }

    self.setEpsilon = function (newEpsI) {
        var newEps = newEpsI / 100;
        if (newEps !== self.epsVisible) {
            self.epsVisible = newEps;
            self.updateDances();
        }
    }

    self.setParameter = function (name, type) {
        var id = 'param' + name;
        if (typeof window[id] === type) {
            self[name.substring(0,1).toLowerCase() + name.substring(1)] = window[id];
        }
    };

    self.init = function () {
        self.setParameter('ShowBPM', 'boolean');
        self.setParameter('ShowMPM', 'boolean');
        self.setParameter('Numerator', 'number');
        self.setParameter('Tempo', 'number');
        self.setParameter('EpsVisible', 'number');

        $('#epsilon').val(self.epsVisible * 100);

        $('#reset').click(function () { self.doReset() });
        $('#count').click(function () { self.doClick() });

        $('#mt1').click(function () { self.setNumerator(1) });
        $('#mt2').click(function () { self.setNumerator(2) });
        $('#mt3').click(function () { self.setNumerator(3) });
        $('#mt4').click(function () { self.setNumerator(4) });

        self.setNumeratorControl(self.numerator, true);

        $('#epsilon').change(function () {
            self.setEpsilon($(this).val());
        });

        var uri = '/api/dance';
        $.getJSON(uri)
            .done(function (data) {
                self.setupDances(data);
            })
            .fail(function (jqxhr, textStatus, err) {
                window.alert(err);
            });

        $(self.mpmId).each(function () {
            // Save current value of element
            $(this).data('oldVal', $(this));

            // Look for changes in the value
            $(this).bind('propertychange keyup input paste', function () {
                // If value has changed...
                if ($(this).data('oldVal') !== $(this).val()) {
                    // Updated stored value
                    $(this).data('oldVal', $(this).val());

                    var newRate = rateFromText($(this).val());
                    if ($.isNumeric(newRate)) {
                        self.updateRate(newRate);
                    }
                    // Else user error here
                }
            });
        });

        $(self.tempoId).each(function () {
            // Save current value of element
            $(this).data('oldVal', $(this));

            // Look for changes in the value
            $(this).bind('propertychange keyup input paste', function () {
                // If value has changed...
                if ($(this).data('oldVal') !== $(this).val()) {
                    // Updated stored value
                    $(this).data('oldVal', $(this).val());

                    var newRate = rateFromText($(this).val());
                    if ($.isNumeric(newRate)) {
                        self.updateRate(roundTempo(newRate / numerator));
                    }
                    // Else user error here
                }
            });
        });

        var epsilon = $('#epsilon');
        if (epsilon.length > 0) {
            epsilon.noUiSlider({
                start: [5],
                step: 1,
                range: { 'min': [1], 'max': [50] }
            });
        }

        window.danceAction = self.danceAction;
        if (self.tempo !== 0) {
            $(self.tempoId).val(self.tempo/self.numerator);
        }
        //$('#tempo').focus(function () { setFocus(true);});
    }

    self.getTempo = function() {
        return $(self.tempoId).val();
    }

    return {
        init: init,
        getTempo: getTempo
    }
}();

var serviceLookup = function () {
    var self = this;

    self.viewModel = {
        track: ko.observable(null),
        song: ko.observable(null),
        tempo: ko.observable(0)
    };

    self.init = function() {
        var lookup = $('#lookup-by-id');
        if (lookup.length) {
            lookup.submit(function (event) {
                event.preventDefault();

                var idControl = $('#idString');
                if (idControl) {
                    var id = idControl.val();
                    if (id) {
                        var idx = id.lastIndexOf('/');
                        if (idx !== -1) {
                            id = id.substring(idx + 1);
                        }
                        $.getJSON('/api/servicetrack/S' + id)
                            .done(function (data) {
                                console.log(data);
                                if (data.hasOwnProperty('TrackId')) {
                                    self.viewModel.track(data);
                                    self.viewModel.song(null);
                                } else {
                                    self.viewModel.track(null);
                                    self.viewModel.song(data);
                                }
                            })
                            .fail(function (jqXhr, textStatus /*,err*/) {
                                console.log(textStatus);
                            });
                    }
                }
            });

            ko.applyBindings(self.viewModel);
        }
    }

    self.setTempo = function(tempo) {
        self.viewModel.tempo(tempo);
    }

    return {
        init: self.init,
        setTempo: self.setTempo
    }
}();

$(document).ready(function () {
    counter.init();
    serviceLookup.init();
});

