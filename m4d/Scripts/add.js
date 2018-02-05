var serviceLookup = function () {
    var self = this;

    self.viewModel = {
        track: ko.observable(null),
        song: ko.observable(null),
        error: ko.observable(null),
        tempo: ko.observable(0)
    };

    self.services = [
        { id: 'i', name: 'itunes', rgx: /[0-9]*/i, idm: /[0-9]*/i },
        { id: 'a', name: 'amazon', rgx: /\/(B[a-z0-9]{9})/i, idm: /B[a-z0-9]{9}/i },
        { id: 's', name: 'spotify', rgx: /([a-z0-9]+)(?:\?si=[a-z0-9 ]*)?$/i, idm: /[a-z0-9]{22}/i }
    ];

    self.serviceFromId = function (id) {
        var ret = '0';
        $.each(self.services, function (i, value) {
            if (id.indexOf(value.name) !== -1) {
                ret = value.id;
                return false;
            }
            return true;
        });
        return ret;
    }

    self.parseId = function (id, rgx) {
        // Our regexes are either full match or with a specified substring
        //  returning the last element should handle both cases.
        var m = rgx.exec(id);
        return m ? m[m.length - 1] : null;
    }

    self.matchId = function (id, idm) {
        var m = id.match(idm);
        return m && m[0] === id;
    }

    self.getService = function (sid) {
        var ret = null;
        $.each(self.services, function (i, value) {
            if (sid === value.id) {
                ret = value;
                return false;
            }
            return true;
        });
        return ret;
    }

    self.getServiceTrack = function (action) {
        this.viewModel.error(null);
        this.viewModel.song(null);
        this.viewModel.track(null);

        var idControl = $('#idString');
        if (!idControl) { return; }

        var buttonId = action.attr('id');
        var service;
        if (buttonId === 'service-') {
            service = action.val();
        } else {
            // Chose something from the dropdown
            service = buttonId.split('-')[1];
            var mb = $('#service-');
            mb.val(service);
            mb.text(action.text());
        }

        var id = idControl.val();
        if (!id) {
            self.viewModel.error('Please enter a music service ID or URL');
            return;
        }
        id = id.trim();
        var inferred = serviceFromId(id);

        if (inferred === '0') {
            if (service === '0') {
                // Loop through the services to see if the actual id matches
                $.each(self.services, function (i, value) {
                    if (self.matchId(id, value.idm)) {
                        service = value.id;
                        return false;
                    }
                    return true;
                });
                if (service === '0') {
                    this.viewModel.error('We did not recognize this as a valid id for any supported service.');
                    return;
                }
            } else {
                // Otherwise we'll do a quick validity check
                if (!self.matchId(id, self.getService(service).idm)) {
                    this.viewModel.error('The Id or Url format doesn\'t match the selected service');
                    return;
                }
            }
        } else {
            // There was both an inferred and an explicit service, error out if the don't match
            if (service !== '0' && service !== inferred) {
                this.viewModel.error('The Id or Url format doesn\'t match the selected service');
                return;
            }
            service = inferred;

            // Since we have an inferred service, we'll assume it's a full url
            var t = self.parseId(id, self.getService(service).rgx);
            if (!t) {
                // But if it isn't we'll grab the tail and check that
                var m = id.match(/[a-z0-9]*$/i);
                if (m && self.matchId(m[0], self.getService(service).idm)) {
                    t = m[0];
                }
            }
            id = t;
            if (id === null) {
                this.viewModel.error('Invalid Id/Url format');
                return;
            }
        }

        if (!id || service === '0') {
            self.viewModel.error('Couldn\'t parse id');
            return;
        }

        $.ajax({
            cache: true,
            success: function (data) {
                if (data.hasOwnProperty('TrackId')) {
                    self.viewModel.track(data);
                    self.viewModel.song(null);
                    $('#service').val(service);
                    $('#purchase').val(data.TrackId);
                    $('#create').submit();
                } else {
                    self.viewModel.track(null);
                    self.viewModel.song(data);
                    $('#id').val(data.SongId);
                    $('#edit').submit();
                }
            },
            error: function (/*jqXhr, textStatus ,err*/) {
                self.viewModel.error('Sorry, we couldn\'t find that song');
            },
            url: '/api/servicetrack/' + service + id
        });
    }

    self.init = function () {
        var lookup = $('#lookup-by-id');
        if (lookup.length) {

            $('.service-lookup').click(function () {
                self.getServiceTrack($(this));
            });

            ko.applyBindings(self.viewModel);
        }
    }

    self.setTempo = function (tempo) {
        self.viewModel.tempo(tempo);
    }

    return {
        init: self.init,
        setTempo: self.setTempo
    }
}();

$(document).ready(function () {
    serviceLookup.init();
});
