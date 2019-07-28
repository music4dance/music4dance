// TODO: Think about what order tags should show up in editor/details page and verify that save still works

var helpers = function () {
    /// Helper functions
    var computeAlbumName = function(idx, name) {
        var ret = 'Albums[' + idx + '].' + name;
        return ret;
    };

    var computeAlbumId = function(idx, name) {
        var ret = 'Albums_' + idx + '__' + name;
        return ret;
    };

    var formatDuration = function(seconds) {
        var m = Math.floor(seconds / 60);
        var s = seconds % 60;
        var sec = s < 10 ? '0' + s.toString() : s.toString();

        return m.toString() + ':' + sec;
    };

    return {
        computeAlbumName: computeAlbumName,
        computeAlbumId: computeAlbumId,
        formatDuration: formatDuration,
        urlMusicService: '/api/musicservice/'
    };
}();

var editor = function () {
    // Forward declare model and mappings
    var viewModel = null;
    var albumMapping = null;
    var ratingMapping = null;
    var songMapping = null;

    var ratingState = {
        UNCHANGED: 'unchanged',
        DELETED: 'deleted',
        CREATED: 'created',
        MODIFIED: 'modified'
    };

    var voteState = {
        DOWN: -1,
        NEUTRAL: 0,
        UP: 1
    };

    var changedHandler = function(f) {
        if (f === undefined) {
            return viewModel.changed;
        } else {
            return viewModel.changed(f);
        }
    };

    var logoFromEnum = function (e) {
        var ret = null;
        switch (e) {
            case 1: ret = '/Content/amazon-logo.png';
                break;
            case 2: ret = '/Content/itunes-logo.png';
                break;
            case 3: ret = '/Content/spotify-logo.png';
                break;
        }

        return ret;
    };

    var purchaseLinksFromTrack = function (track) {
        var ret = null;
        if (track.SongLink) {
            ret = track.SongLink;
        }
        else if (track.AlbumLink) {
            ret = track.AlbumLink;
        }
        return [ret];
    };

    // Make a field id from an alt-field id:  e.g. alt-title -> Title
    var altToField = function (altId) {
        var id = altId[4].toUpperCase() + altId.substring(5);
        return id;
    };

    // Swap the text of the clicked link with the parent field's value
    var replaceValue = function (self) {
        var id = self[0].parentNode.id;
        var field = $('#' + altToField(id));
        var text = self.text();
        self.text(field.val());
        field.val(text);
    };

    var addValue = function (id, val) {
        if (!val) {
            return;
        }

        var self = $('#' + id);
        var field = $('#' + altToField(id));

        var oldVal = field.val();
        // We really want this to be approximately equal so number == string of same value
        // ReSharper disable once CoercedEqualsUsing
        if (val === oldVal) {
            return;
        }

        var dup = false;
        $('#' + id + ' a').each(function () {
            if ($(this).text() === val) {
                dup = true;
                return false;
            }
            return true;
        });

        if (!dup) {
            var node = '<a href="#" role="button" class="btn btn-link">' + oldVal + '</a>';
            self.append(node);
            field.val(val);
        }
    };

    var addPurchaseLink = function (link, olist) {
        if (link) {
            // TODO: Has to be a cleaner way to find existence ($.inArray isn't working
            //  possibly because we're mapped - may be that going back and figuring
            //  out how to get KO Mapping to mapp the PurchaseLink array but not
            //  the objects is the way to go...

            for (var i = 0; i < olist().length; i++) {
                if (link.Link === olist()[i].Link()) {
                    return; // Already have this link
                }
            }

            var olink = ko.mapping.fromJS(link);
            if ($.inArray(olink, olist()) === -1) {
                olist.push(olink);
            }
        }
    };

    var normalizeName = function(name) {
        name = name.toLowerCase();
        name = name.replace(/[^a-z0-9]/g, '');
        return name;
    };

    // Track object
    // ReSharper disable once InconsistentNaming
    var Track = function (data) {
        var self = this;

        $.extend(true, this, data);

        this.durationFormatted = ko.pureComputed(function () {
            return helpers.formatDuration(self.Duration);
        }, this);

        this.serviceLogo = ko.pureComputed(function () {
            return logoFromEnum(self.Service);
        }, this);

        this.FullPurchaseInfo = ko.pureComputed(function () {
            return self.PurchaseInfoArray().join(';');
        }, this);

        this.MarketString = ko.pureComputed(function () {
            return !self.AvailableMarkets ? '' : '[' + self.AvailableMarkets.join() + ']';
        }, this);

        this.PurchaseInfoArray = ko.pureComputed(function () {
            var ret = [];

            var tpis = this.PurchaseInfo.split(';');

            for (var i = 0; i < tpis.length; i++) {
                var v = tpis[i];
                if (v.match(/[a-z]S=.*/i)) {
                    v += self.MarketString();
                }
                ret.push(v);
            }

            return ret;
        }, this);
    };

    // Rating object
    // ReSharper disable once InconsistentNaming
    var Rating = function (data, parent) {
        var self = this;

        self.song = parent;
        self.action = 'change';

        if (!data.CurrentUserTags) {
            data.CurrentUserTags = { Summary: '', Tags: [] };
        }

        self.extraTagTypes = [{ name: 'Style', label: 'Style' }];

        ko.mapping.fromJS(data, ratingMapping, this);

        self.isExplicit = ko.pureComputed(function () {
            return self.song.TagSummary.hasTag(data.DanceName, 'Dance');
        }, this);

        self.removeDance = function() {
            if (self.state() !== ratingState.CREATED) {
                self.song.TagSummary.addTag('^' + self.DanceName(), 'Dance');
            }
            self.song.TagSummary.removeTag(self.DanceName(), 'Dance');
            self.song.DanceRatings.remove(self);
        };

        self.toggleVote = function () {
            switch (self.vote()) {
            case voteState.UP:
                self.song.TagSummary.addTag('!' + self.DanceName(), 'Dance');
                self.song.TagSummary.removeTag(self.DanceName(), 'Dance');
                // Remove Rating if it was created in this session
                if (self.state() === ratingState.CREATED) {
                    self.song.DanceRatings.remove(self);
                }
                break;
            case voteState.DOWN:
                self.song.TagSummary.removeTag('!' + self.DanceName(), 'Dance');
                break;
            default:
                self.song.TagSummary.addTag(self.DanceName(), 'Dance');
                break;
            }
        };

        self.voteImage = ko.pureComputed(function () {
            var img;
            switch (self.vote()) {
            case voteState.UP:
                img = '';
                break;
            case voteState.DOWN:
                img = 'broken-';
                break;
            default:
                img = 'outline-';
                break;
            }
            return '/content/dance-' + img + 'icon.png';
        }, this);

        self.voteName = ko.pureComputed(function () {
            switch (self.vote()) {
            case voteState.UP:
                return 'You like to dance ' + self.DanceName() + ' to this song';
            case voteState.DOWN:
                return 'You don\'t like to dance ' + self.DanceName() + ' to this song';
            default:
                return 'You haven\'t voted';
            }
        }, this);

        self.vote = ko.pureComputed(function () {
            if (self.song.TagSummary.findUserTag(self.DanceName(), 'Dance')) {
                return voteState.UP;
            }
            else if (self.song.TagSummary.findUserTag('!' + self.DanceName(), 'Dance')) {
                return voteState.DOWN;
            } else {
                return voteState.NEUTRAL;
            }
        }, this);

        self.liketip = ko.pureComputed(function () {
            return self.voteName() + ': Click to change your vote.';
        }, this);

        if (!('state' in data)) {
            self.state = ko.observable(ratingState.UNCHANGED);
        }

        self.link = ko.pureComputed(function () {
            return '/dances/' + self.DanceName().toLowerCase().replace(' ', '-');
        }, this);

        self.tooltip = ko.pureComputed(function () {
            return self.Weight() + ' votes where the top song in the ' + self.DanceName() + ' category has ' + self.Max() + ' votes.';
        }, this);

        self.lookupName = ko.pureComputed(function () {
            //return self.DanceName().replace(/\s/g, '').toLowerCase();
            return self.DanceName().toLowerCase();
        }, this);

        self.danceTag = ko.pureComputed(function () {
            return self.isExplicit() ? 'dance-tag' : '';
        }, this);

        self.changed = changedHandler;
        self.changeText = function() { return 'your list of tags.'; }
    };

    // Album object
    // ReSharper disable once InconsistentNaming
    var Album = function (data) {
        var self = this;

        //{'copy': 'PurchaseLinks' }
        ko.mapping.fromJS(data, {}, this);

        self.addPurchase = function (track) {
            //var pi = self.PurchasInfo();
            //var r = /[AIX][SA]=[^;]*/g, match;

            //while (match)

            // First do the string based purchase info
            var pi = self.PurchaseInfo();

            if (!pi) {
                self.PurchaseInfo(track.FullPurchaseInfo());
            }
            else if (track) {
                // get rid of possible terminal ;
                if (pi[pi.length - 1] === ';') {
                    pi = pi.substring(0, pi.length - 1);
                }

                var tpis = track.PurchaseInfoArray();
                for (var i = 0; i < tpis.length; i++) {
                    if (pi && pi.indexOf(tpis[i]) === -1) {
                        pi += ';' + tpis[i];
                    }
                }

                self.PurchaseInfo(pi);
            }

            if (!track)
                return;

            // Then add in the purchase links
            addPurchaseLink(track.SongLink, self.PurchaseLinks);
            //addPurchaseLink(track.AlbumLink, self.PurchaseLinks());

            // Also add in track number if it wasn't there before
            if (!self.Track()) {
                self.Track(track.TrackNumber);
            }
        };

        self.matchTrack = function(track) {
            return (!self.Track() || self.Track() === track.TrackNumber) &&
                normalizeName(self.Name()) === normalizeName(track.Album);
        };
    };

    // Song object
    // ReSharper disable once InconsistentNaming
    var Song = function (data) {
        var self = this;

        self.action = 'change';

        if (!data.CurrentUserTags) {
            data.CurrentUserTags = { Summary: '', Tags: [] };
        }

        self.extraTagTypes = [{ name: 'Music', label: 'Musical Genre' }];

        ko.mapping.fromJS(data, albumMapping, this);

        self.hasInferred = ko.pureComputed(function () {
            for (var i = 0; i < self.DanceRatings().length; i++) {
                if (!self.DanceRatings()[i].isExplicit()) {
                    return true;
                }
            }
            return false;
        }, this);

        // Alubm Management
        self.nextIndex = function () {
            var max = 1;
            for (var i = 0; i < self.Albums().length; i++) {
                var a = self.Albums()[i];
                max = Math.max(max, a.Index() + 1);
            }
            return max;
        };

        self.newAlbum = function () {
            var idx = self.nextIndex();
            var temp = ko.mapping.fromJS({ Index: idx, Name: null, Publisher: null, Track: null, PurchaseInfo: null, PurchaseLinks: null }, albumMapping);
            self.Albums.push(temp);
        };

        self.removeAlbum = function (data, event) {
            self.Albums.mappedRemove({ Index: data.Index });
            event.preventDefault();
        };

        self.promoteAlbum = function (data, event) {
            var temp = self.Albums.mappedRemove({ Index: data.Index });
            self.Albums.unshift(temp[0]);
            event.preventDefault();
        };

        self.findAlbum = function (track) {
            for (var i = 0; i < viewModel.song.Albums().length; i++) {
                var a = viewModel.song.Albums()[i];
                if (a.matchTrack(track)) {
                    return a;
                }
            }

            return null;
        };

        // Track Management
        self.chooseTrack = function (track, event) {
            event.preventDefault();

            // Update the track info
            var name = track.Album;
            var num = track.TrackNumber;

            var a = self.findAlbum(track);

            if (a) {
                a.addPurchase(track);
            }
            else {
                var idx = self.nextIndex();
                var temp = new Album({ Index: idx, Name: name, Track: num, Publisher: '', PurchaseInfo: track.FullPurchaseInfo(), PurchaseLinks: purchaseLinksFromTrack(track) });
                self.Albums.push(temp);
            }

            // Now add in the extra top level info if empty
            //  or replace and put in a change back button if not

            addValue('alt-title', track.Name);
            addValue('alt-artist', track.Artist);
            addValue('alt-length', track.durationFormatted());
            addValue('alt-tempo', track.Tempo);

            // Finally handle genre
            if (track.Genre) {
                self.TagSummary.addTag(track.Genre, 'Music');
            }
        };

        // Voting Management
        self.voteImage = ko.pureComputed(function () {
            var img;
            switch (self.CurrentUserLike()) {
                default:
                    img = 'outline-';
                    break;
                case true:
                    img = '';
                    break;
                case false:
                    img = 'broken-';
                    break;
            }
            return '/content/heart-' + img + 'icon.png';
        }, this);

        self.toggleVote = function() {
            switch (self.CurrentUserLike()) {
            default:
                self.CurrentUserLike(true);
                break;
            case true:
                self.CurrentUserLike(false);
                break;
            case false:
                self.CurrentUserLike(null);
                break;
            }
            self.changed(true);
        };

        self.voteText = function() {
            var ret = 'null';
            switch (self.CurrentUserLike()) {
            case true:
                ret = 'true';
                break;
            case false:
                ret = 'false';
                break;
            }
            return ret + ':Like';
        };

        self.serialize = function() {
            return self.voteText() + '|' + self.TagSummary.serializeUser();
        };

        self.liketip = ko.pureComputed(function () {
            switch (self.CurrentUserLike()) {
                case true: return 'You have liked this song, click to dislike';
                case false: return 'You have disliked this song, click to reset';
                default: return 'Click to like/dislike this song.';
            }
        }, this);

        self.changed = changedHandler;
        self.changeText = function () { return 'your list of tags.'; }
    };

    // EditPage object
    // ReSharper disable once InconsistentNaming
    var EditPage = function (data) {
        var self = this;

        ko.mapping.fromJS(data, songMapping, this);

        self.canEdit = ko.observable(canEdit);
        self.canTag = ko.observable(canTag);
        self.showTag = ko.observable(canTag);

        self.tagSuggestions = ko.observable(tagChooser.tagSuggestions('tag-editing',tagChooser.currentSuggestion('Change')));

        self.getRatings = function () {
            var source = { Tags: [{ Id: '', Tags: self.song.serialize() }] };

            for (var i = 0; i < self.song.DanceRatings().length; i++) {
                var rating = self.song.DanceRatings()[i];
                var user = rating.TagSummary.serializeUser();
                if (user)
                    source.Tags.push({ Id: rating.DanceId(), Tags: user });
            }

            return source;
        };

        self.saveRatings = function () {
            var uri = '/api/updateratings/' + self.song.SongId();

            var source = self.getRatings();

            $.ajax({
                type: 'POST',
                dataType: 'json',
                url: uri,
                data: source,
                success: function (/*data*/) {
                    self.changed(false);
                },
                error: function (error) {
                    var jsonValue = jQuery.parseJSON(error.responseText);
                    window.alert('An error has occurred while saving the new part source: ' + jsonValue);
                }
            });
        };

        self.showTagModal = function (event) {
            var song = self.song;

            var button = $(event.relatedTarget);
            var danceId = button.data('danceid');

            var obj = song;
            var titleExtra = '';
            if (danceId) {
                for (var i = 0; i < song.DanceRatings().length; i++) {
                    var t = song.DanceRatings()[i];
                    if (t.DanceId() === danceId) {
                        obj = t;
                        titleExtra = ' for ' + t.DanceName();
                        break;
                    }
                }
            }

            tagChooser.bindModal(self.tagSuggestions(), obj, button, $(event.currentTarget), titleExtra);
        };

        self.toggleVote = function() {
            //TODO: Take another run at figuring out why we can't do this inderection in the html
            self.song.toggleVote();
        };
    };

    albumMapping = {
        'Albums': {
            create: function(options) {
                return new Album(options.data);
            },
            key: function(data) {
                return ko.utils.unwrapObservable(data.Index);
            }
        },
        'DanceRatings': {
            create: function(options) {
                return new Rating(options.data, options.parent);
            }
        },
        'TagSummary': {
            create: function(options) {
                return tagChooser.tagSummary(options.data, options.parent);
            }
        }
    };

    ratingMapping = {
        'TagSummary': {
            create: function (options) {
                return tagChooser.tagSummary(options.data, options.parent);
            }
        }
    };

    songMapping = {
        'song': {
            create: function (options) {
                return new Song(options.data);
            }
        }
    };

    var pageMapping = {
        create: function (options) {
            return new EditPage(options.data);
        }
    };

    var trackMapping = {
        'tracks': {
            create: function (options) {
                return new Track(options.data);
            }
        }
    };

    var getServiceInfo = function (service) {
        var uri = helpers.urlMusicService + viewModel.song.SongId() + '?region=US&service=' + service.toString() + '&Title=';
        var t = $('#search').val();
        if (t.length > 0) {
            uri += encodeURI(t);
        }
        else {
            uri += encodeURI($('#Title').val()) + '&Artist=' + encodeURI($('#Artist').val());
        }

        var aid = '#' + helpers.computeAlbumId(0, 'Name');
        var afield = $(aid);
        if (afield.length) {
            uri += '&Album=' + afield.val();
        }

        $.getJSON(uri)
            .done(function (data) {
                viewModel.tracks.removeAll();
                var m = { tracks: data };
                viewModel = ko.mapping.fromJS(m, trackMapping, viewModel);
            })
            .fail(function (jqXhr, textStatus /*,err*/) {
                console.log(textStatus);
            });
    };

    var hasDanceRatings = function() {
        return viewModel.song.DanceRatings().length > 0;
    };

    var updateUserTags = function() {
        var t = JSON.stringify(viewModel.getRatings());
        $('#userTags').val(t);
        viewModel.changed(false);
    };

    var danceAction = function (id) {
        var option = $('#addDance > option[value=' + id + ']');
        var name = option.text();

        var rating = null;
        for (var i = 0; i < viewModel.song.DanceRatings().length; i++) {
            if (viewModel.song.DanceRatings()[i].DanceId() === id) {
                rating = viewModel.song.DanceRatings()[i];
            }
        }

        viewModel.song.TagSummary.addTag(name, 'Dance');

        if (rating) {
            rating.state(ratingState.MODIFIED);
        } else {
            viewModel.song.DanceRatings.push(new Rating(
            {
                DanceId: id,
                DanceName: name,
                Weight: 1,
                Max: 5,
                Badge: 'rating-2',
                TagSummary: { Summary: '', Tags: [] },
                state: ratingState.CREATED
            },
            viewModel.song));
        }

        $('#dance-error').hide();
    };


    var unloadWarning = function() {
        if (viewModel.changed()) {
            return 'You have unsaved changes on this page.';
        }
        return undefined;
    };

    var confirmUserUndo = function () {
        return confirm('You are about to permanently undo all of your changes to this song.');
    };

    var showTagModal = function(event) {
        viewModel.showTagModal(event);
    };

    var init = function() {
        var data = { tracks: [], song: song, changed: false };
        viewModel = ko.mapping.fromJS(data, pageMapping);

        tagChooser.setupModal(viewModel.tagSuggestions,
            showTagModal,
            { width: '500px', create_option: true, persistent_create_option: true, skip_no_results: true });

        ko.applyBindings(viewModel);


        if (window.paramNewTempo) {
            addValue('alt-tempo', window.paramNewTempo);
        }

        $('#toggle-count-display').removeClass('glyphicon-arrow-down');
        $('#toggle-count-display').addClass('glyphicon-arrow-up');
    };

    return {
        init: init,
        getServiceInfo: getServiceInfo,
        hasDanceRatings: hasDanceRatings,
        showTagModal: showTagModal,
        replaceValue: replaceValue,
        danceAction: danceAction,
        updateUserTags: updateUserTags,
        unloadWarning: unloadWarning,
        confirmUserUndo: confirmUserUndo
    };
}();

var danceAction = editor.danceAction;

$(document).ready(function () {
    $('#toggle-count').click(function() {
        var visible = $('#counter-control').is(':visible');
        if (visible) {
            $('#counter-control').hide();
            $('#toggle-count-display').removeClass('glyphicon-arrow-up');
            $('#toggle-count-display').addClass('glyphicon-arrow-down');
        } else {
            $('#counter-control').show();
            $('#toggle-count-display').removeClass('glyphicon-arrow-down');
            $('#toggle-count-display').addClass('glyphicon-arrow-up');
        }
    });

    // Dances initialization
    $('#addDance').chosen({ allow_single_deselect: true, search_contains: true });
    $('#addDance').chosen().change(function(event, data) {
        editor.danceAction(data.selected);
    });

    $('body').tooltip({ selector: '[data-show=tooltip]' });

    $('#load-itunes').click(function () { editor.getServiceInfo('I'); });
    $('#load-spotify').click(function () { editor.getServiceInfo('S'); });
    $('#load-amazon').click(function () { editor.getServiceInfo('A'); });
    $('#load-all').click(function () { editor.getServiceInfo('_'); });

    $('#alt-title').on('click', '.btn', null, function (event) {
        event.preventDefault();
        editor.replaceValue($(this));
    });
    $('#alt-artist').on('click', '.btn', null, function (event) {
        event.preventDefault();
        editor.replaceValue($(this));
    });
    $('#alt-length').on('click', '.btn', null, function (event) {
        event.preventDefault();
        editor.replaceValue($(this));
    });
    $('#alt-tempo').on('click', '.btn', null, function (event) {
        event.preventDefault();
        editor.replaceValue($(this));
    });

    $(document).ajaxSend(function (event, request, settings) {
        if (settings.url.indexOf(helpers.urlMusicService) === 0) {
            $('#loading-indicator').show();
        }
    });

    $(document).ajaxComplete(function (event, request, settings) {
        if (settings.url.indexOf(helpers.urlMusicService) === 0) {
            $('#loading-indicator').hide();
        }
    });


    //$('#save').click(function () {
    //    editor.updateUserTags();
    //});

    $('#song').submit(function(event) {
        editor.updateUserTags();
        if (!editor.hasDanceRatings() && !window.isPremium) {
            $('#dance-error').show();
            event.preventDefault();
        }
    });

    $('#userUndo').click(function () { return editor.confirmUserUndo(); });

    editor.init();

    window.onbeforeunload = editor.unloadWarning;

    $('.chosen-select').chosen({ width: '500px' });
});
