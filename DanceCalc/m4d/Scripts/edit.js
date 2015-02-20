var viewModel = null;

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
            TagSummary: {Summary:'',Tags:[]},
            state: ratingState.CREATED
        },
        viewModel.song));
    }
};

/// Helper functions
var computeAlbumName = function (idx, name) {
    var ret = 'Albums[' + idx + '].' + name;
    return ret;
};

var computeAlbumId = function (idx, name) {
    var ret = 'Albums_' + idx + '__' + name;
    return ret;
};

var formatDuration = function (seconds) {
    var m = Math.floor(seconds / 60);
    var s = seconds % 60;
    var sec = (s < 10) ? ('0' + s.toString()) : s.toString();

    return m.toString() + ':' + sec;
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
        case 4: ret = '/Content/xbox-logo.png';
            break;
    }

    return ret;
};

var purchaseLinksFromTrack = function (track) {
    var ret = null;
    if (track.SongLink != null) {
        ret = track.SongLink;
    }
    else if (track.AlbumLink != null) {
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
    var id = self.context.parentElement.id;
    var field = $('#' + altToField(id));
    var text = self.text();
    self.text(field.val());
    field.val(text);
};

var addValue = function (id, val) {
    if (val == null) {
        return;
    }

    var self = $('#' + id);
    var field = $('#' + altToField(id));

    var oldVal = field.val();
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

// Track object
var Track = function (data) {
    var self = this;

    $.extend(true, this, data);

    this.durationFormatted = ko.pureComputed(function () {
        return formatDuration(this.Duration);
    }, this);

    this.serviceLogo = ko.pureComputed(function () {
        return logoFromEnum(this.Service);
    }, this);

    this.FullPurchaseInfo = ko.pureComputed(function () {
        return PurchaseInfoArray().join(';');
    }, this);

    this.MarketString = ko.pureComputed(function () {
        return (self.AvailableMarkets === null) ? '' : '[' + this.AvailableMarkets.join() + ']';
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

var trackMapping = {
    'tracks': {
        create: function (options) {
            return new Track(options.data);
        }
    }
};

var addPurchaseLink = function (link, olist) {
    if (link != null) {
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

// Rating object
var Rating = function(data,parent) {
    var self = this;

    self.song = parent;

    if (data.CurrentUserTags == null) {
        data.CurrentUserTags = { Summary: '', Tags: [] };
    }

    ko.mapping.fromJS(data, ratingMapping, this);

    self.isExplicit = ko.pureComputed(function() {
        return self.song.TagSummary.hasTag(data.DanceName, 'Dance');
    }, this);

    self.toggleVote = function() {
        switch (self.vote()) {
            case voteState.UP:
                self.song.TagSummary.addTag('-' + self.DanceName(), 'Dance');
                self.song.TagSummary.removeTag(self.DanceName(), 'Dance');
                break;
            case voteState.DOWN:
                self.song.TagSummary.removeTag('-' + self.DanceName(), 'Dance');
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
        return '/content/heart-' + img + 'icon.png';
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
        else if (self.song.TagSummary.findUserTag('-' + self.DanceName(), 'Dance')) {
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

    self.tooltip = ko.pureComputed(function() {
        return self.Weight() + ' votes where the top song in the ' + self.DanceName() + ' category has ' + self.Max() + ' votes.';
    }, this);

    self.lookupName = ko.pureComputed(function() {
        //return self.DanceName().replace(/\s/g, '').toLowerCase();
        return self.DanceName().toLowerCase();
    }, this);

    self.danceTag = ko.pureComputed(function() {
        return self.isExplicit() ? 'dance-tag' : '';
    }, this);
};

// Album object
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

        if (pi == null) {
            self.PurchaseInfo(track.FullPurchaseInfo());
        }
        else if (track != null) {
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

        // Then add in the purchase links
        addPurchaseLink(track.SongLink, self.PurchaseLinks);
        //addPurchaseLink(track.AlbumLink, self.PurchaseLinks());

        // Also add in track number if it wasn't there before
        if (self.Track() == null) {
            self.Track(track.TrackNumber);
        }
    };
};

Album.prototype.matchTrack = function (track)
{
    return (this.Track() == null || this.Track() === track.TrackNumber) && this.Name().toLowerCase() === track.Album.toLowerCase();
};

var Tag = function(value,parent) {
    var self = this;

    var values = value.split(':');

    self.tag = ko.observable(values.length > 0 ? values[0] : null);
    self.cat = ko.observable(values.length > 1 ? values[1] : null);
    self.cnt = ko.observable(values.length > 2 ? values[2] : 1);

    self.parent = parent;

    self.userTags = ko.pureComputed(function() {
        return self.parent.CurrentUserTags;
    });

    self.value = ko.pureComputed(function () {
        return self.tag() + ':' + self.cat();
    }, this);

    self.url = ko.pureComputed(function () {
        return '/song/tags?tags=' + encodeURIComponent(self.tag()) + ':' + encodeURIComponent(self.cat());
    }, this);

    self.isUserTag = ko.pureComputed(function() {
        var val = self.tag() + ':' + self.cat();
        return self.userTags().Tags.indexOf(val) !== -1;
    }, this);

    self.tagClass = ko.pureComputed(function () {
        var ret = self.isUserTag() ? 'glyphicon-tag' : 'glyphicon-plus-sign';
        return ret;
    }, this);

    // TODO: Can't seem to get this to fire even with what appears to be an identical pattern to other tool tips...
    //self.tooltip = ko.pureComputed(function () {
    //    return 'Click to ' + (self.isUserTag() ? 'remove from' : 'add to') + ' your tags';
    //}, this);

    self.toggleUser = function() {
        var value = self.value();
        var count = self.cnt();
        if (self.isUserTag()) {
            self.userTags().Tags.remove(value);
            count -= 1;
        } else {
            self.userTags().Tags.push(value);
            count += 1;
        }
        self.cnt(count);
        if (count === 0) {
            parent.TagSummary.Tags.remove(self);
        }

        viewModel.changed(true);
    };
};

var TagType = function(summary, name, label) {
    var self = this;

    self.summary = summary;
    self.name = name;
    self.label = label;

    self.list = ko.computed(function() {
        return ko.utils.arrayFilter(self.summary.Tags(), function(tag) {
            return tag.cat() === self.name;
        });
    }, this);

    self.tooltip = ko.pureComputed(function () {
        return 'Add ' + self.label + ' tags.';
    }, this);

    self.nameLower = function() {return name.toLowerCase()};
};

// Tag Summary Object
var TagSummary = function(data, parent, forSong) {
    var self = this;
    self.parent = parent;

    self.tagsFromSummary = function(summary) {
        if (!summary) return [];

        var list = [];
        var tcs = summary.split('|');
        for (var i = 0; i < tcs.length; i++) {
            list.push(new Tag(tcs[i].trim(), self.parent));
        }
        return list;
    };

    self.Summary = ko.observable(data.Summary);
    self.Tags = ko.observableArray(self.tagsFromSummary(data.Summary));
    self.userTags = ko.pureComputed(function () {
        return self.parent.CurrentUserTags;
    }, this);

    // Build the tag types
    self.tagTypes = [];
    if (forSong) self.tagTypes.push(new TagType(self, 'Music', 'Musical Genre'));
    if (!forSong) self.tagTypes.push(new TagType(self, 'Style', 'Style'));
    self.tagTypes.push(new TagType(self, 'Tempo', 'Tempo'));
    self.tagTypes.push(new TagType(self, 'Other', 'Other'));

    self.danceId = ko.pureComputed(function() {
        if (forSong) return '';
        return self.parent.DanceId();
    });

    self.hasTag = function(value, kind) {
        var s = value + ':' + kind;
        for (var i = 0; i < self.Tags().length; i++) {
            if (self.Tags()[i].value() === s) {
                return true;
            }
        }
        return false;
    };

    self.getTagType = function (tt) {

        for (var i = 0; i < self.tagTypes.length; i++) {
            if (self.tagTypes[i].name === tt)
                return self.tagTypes[i];
        }
        return null;
    };

    self.findTag = function(tag, cat) {
        var tagO = null;
        for (var i = 0; i < self.Tags().length; i++) {
            var tagT = self.Tags()[i];

            if (tagT.tag() === tag && tagT.cat() === cat) {
                tagO = tagT;
                break;
            }
        }
        return tagO;
    };

    self.findUserTag = function(tag, cat) {
        var tagO = self.findTag(tag, cat);
        return (tagO && tagO.isUserTag()) ? tagO : null;
    };
    self.removeTag = function (tag, cat) {
        // Is there an existing match?
        var tagO = self.findTag(tag, cat);

        // If there isn't an existing tag, we're done
        if (!tagO) return;

        self.userTags().Tags.remove(tag + ':' + cat);
        self.Tags.remove(tagO);

        viewModel.changed(true);
    };

    self.addTag = function(tag, cat) {
        // Is there an existing match?
        var tagO = self.findTag(tag, cat);

        // If there isn't an existing tag, create it
        var value = tag + ':' + cat;
        if (!tagO) {
            tagO = new Tag(value, self.parent);
            self.Tags.push(tagO);
        } else {
            tagO.cnt(tagO.cnt() + 1);
        }

        // Add tags to the associated userTag list if needed
        if (!tagO.isUserTag()) {
            self.userTags().Tags.push(value);
        }

        viewModel.changed(true);
    };

    self.addTags = function(name) {
        // Create usable tag list from the input
        var tagString = viewModel.newTags();
        var tagsT = tagString.split(',');

        for (var i = 0; i < tagsT.length; i++) {
            var tag = tagsT[i].trim();
            if (tag) {
                self.addTag(tag, name);
            }
        }

        viewModel.newTags('');
    };

    self.serializeUser = function() {
        var s = '';
        var sep = '';
        for (var i = 0; i < self.userTags().Tags().length; i++) {
            var tag = self.userTags().Tags()[i];
            s += sep + tag;
            sep = '|';
        }
        return s;
    };
}; // Song object
var Song = function (data) {
    var self = this;

    if (data.CurrentUserTags == null) {
        data.CurrentUserTags = { Summary: '', Tags: [] };
    }
    ko.mapping.fromJS(data, albumMapping, this);

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
    self.chooseTrack = function (track, event) {
        event.preventDefault();

        // Update the track info
        var name = track.Album;
        var num = track.TrackNumber;

        var a = self.findAlbum(track);

        if (a != null) {
            a.addPurchase(track);
        }
        else {
            var idx = self.nextIndex();
            var temp = new Album({ Index: idx, Name: name, Track: num, Publisher: '', PurchaseInfo: track.PurchaseInfo, PurchaseLinks: purchaseLinksFromTrack(track) });
            self.Albums.push(temp);
        }

        // Now add in the extra top level info if empty
        //  or replace and put in a change back button if not

        addValue('alt-title', track.Name);
        addValue('alt-artist', track.Artist);
        addValue('alt-length', track.durationFormatted());
        addValue('alt-tempo', track.Tempo);

        // Finally handle genre
        if (track.Genre != null) {
            var gnew = track.Genre + ':Music';
            var gval = $('#editTags').val();
            if (gval == null) {
                gval = '';
            }
            var glist = gval.toLowerCase().split('|');

            if (glist.indexOf(gnew.toLowerCase()) === -1) {
                if (gval.length > 0) {
                    gval += '|';
                }
                gval += gnew;
            }

            $('#editTags').val(gval);
        }
    };

}; // EditPage object
var EditPage = function(data) {
    var self = this;

    ko.mapping.fromJS(data, songMapping, this);

    self.canEdit = ko.observable(canEdit);

    self.getRatings = function() {
        var source = { Tags: [{ Id: '', Tags: self.song.TagSummary.serializeUser() }] };

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
                window.jError('An error has occurred while saving the new part source: ' + jsonValue, { TimeShown: 3000 });
            }
        });
    };
};

var albumMapping = {
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
            return new Rating(options.data,options.parent);
        }
    },
    'TagSummary': {
        create: function(options) {
            return new TagSummary(options.data,options.parent,true);
        }
    }
};

var ratingMapping = {
    'TagSummary': {
        create: function (options) {
            return new TagSummary(options.data, options.parent,false);
        }
    }
};

var songMapping = {
    'song': {
        create: function (options) {
            return new Song(options.data);
        }
    }
};
var pageMapping = {
    create: function(options) {
        return new EditPage(options.data);
    }
};

var getServiceInfo = function(service) {
    var uri = '/api/musicservice/' + viewModel.song.SongId() + '?service=' + service.toString() + '&Title=';
    var t = $('#search').val();
    if (t.length > 0)
    {
        uri += encodeURI(t);
    }
    else
    {
        uri += encodeURI($('#Title').val()) + '&Artist=' + encodeURI($('#Artist').val());
    }

    var aid = '#' + computeAlbumId(0,'Name');
    var afield = $(aid);
    if (afield.length)
    {
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
$(document).ready(function () {
    $('#counter-control').hide();
    $('#toggle-count').click(function () {
        var visible = $('#counter-control').is(':visible');
        if (visible)
        {
            $('#counter-control').hide();
            $('#toggle-count-display').removeClass('glyphicon-arrow-up');
            $('#toggle-count-display').addClass('glyphicon-arrow-down');
        }
        else
        {
            $('#counter-control').show();
            $('#toggle-count-display').removeClass('glyphicon-arrow-down');
            $('#toggle-count-display').addClass('glyphicon-arrow-up');
        }
    });

    // Dances initialization
    $('#addDance').chosen({ allow_single_deselect: true, search_contains: true });
    $('#addDance').chosen().change(function (event, data) {
        danceAction(data.selected);
    });

    $('body').tooltip({ selector: '[data-show=tooltip]' });

    $('#addTags').on('show.bs.modal', function(event) {
        var button = $(event.relatedTarget);
        var category = button.data('category'); // Extract info from data-* attributes
        var danceId = button.data('danceid');

        var obj = viewModel.song;
        if (danceId) {
            for (var i = 0; i < viewModel.song.DanceRatings().length; i++) {
                var t = viewModel.song.DanceRatings()[i];
                if (t.DanceId() === danceId) {
                    obj = t;
                    break;
                }
            }
        }

        var tagType = obj.TagSummary.getTagType(category);
        var modal = $(this);
        modal.find('.modal-title').text('Add ' + tagType.label + ' tags');

        var okay = modal.find('#addTagsOkay');
        okay.unbind('click.addtags');
        okay.bind('click.addtags', function () {
            viewModel.changed(true);
            obj.TagSummary.addTags(category);
        });
    });

    $('#load-xbox').click(function () { getServiceInfo('X'); });
    $('#load-itunes').click(function () { getServiceInfo('I'); });
    $('#load-spotify').click(function () { getServiceInfo('S'); });
    $('#load-amazon').click(function () { getServiceInfo('A'); });
    $('#load-all').click(function () { getServiceInfo('_'); });

    $('#alt-title').on('click', '.btn', null, function (event) {
        event.preventDefault();
        replaceValue($(this));
    });
    $('#alt-artist').on('click', '.btn', null, function (event) {
        event.preventDefault();
        replaceValue($(this));
    });
    $('#alt-length').on('click', '.btn', null, function (event) {
        event.preventDefault();
        replaceValue($(this));
    });
    $('#alt-tempo').on('click', '.btn', null, function (event) {
        event.preventDefault();
        replaceValue($(this));
    });

    $(document).ajaxSend(function (/*event, request, settings*/) {
        $('#loading-indicator').show();
    });

    $(document).ajaxComplete(function (/*event, request, settings*/) {
        $('#loading-indicator').hide();
    });

    $('#save').click(function () {
        var t = JSON.stringify(viewModel.getRatings());
        $('#userTags').val(t);
    });

    var data = { tracks: [], song: song, newTags:'', changed:false };
    viewModel = ko.mapping.fromJS(data, pageMapping);

    ko.applyBindings(viewModel);

    $('.chzn-select').chosen({ width: '500px' });
});
