function danceAction(id) {
    Debug.write("Dance Id=" + id + "\r\n");
    $('#addDances option[value="' + id + '"]').attr('selected', 'selected');
    $('#addDances').trigger('chosen:updated');

}

/// Helper functions

var computeAlbumName = function (idx, name) {
    var ret = "Albums[" + idx + "]." + name;
    return ret;
}

var computeAlbumId = function (idx, name) {
    var ret = "Albums_" + idx + "__" + name;
    return ret;
}

var formatDuration = function(seconds)
{
    var m = Math.floor(seconds / 60);
    var s = seconds % 60;
    var sec = (s < 10) ? ("0" + s.toString()) : s.toString();

    return m.toString() + ":" + sec;
}

var logoFromEnum = function(e)
{
    var ret = null;
    switch (e)
    {
        case 1: ret = "/Content/amazon-logo.png";
            break;
        case 2: ret = "/Content/itunes-logo.png";
            break;
        case 3: ret = "/Content/xbox-logo.png";
            break;
        }

    return ret;
}
var purchaseLinksFromTrack = function (track) {
    var ret = null;
    if (track.SongLink != null)
    {
        ret = track.SongLink;
    }
    else if (track.AlbumLink != null)
    {
        ret = track.AlbumLink;
    }
    return [ret];
}


// Track object
var Track = function(data)
{
    //var mapping = {};//{'observe': ["Name"]};
    //ko.mapping.fromJS(data, mapping, this);
    $.extend(true, this, data);

    this.durationFormatted = ko.computed(function () {
        return formatDuration(this.Duration);
    }, this);

    this.serviceLogo = ko.computed(function () {
        return logoFromEnum(this.Service);
    }, this);
}

var trackMapping = {
    'tracks': {
        create: function (options) {
            return new Track(options.data);
        }
    }
};

// Album object
var Album = function (data) {
    var self = this;

    ko.mapping.fromJS(data, {}, this);

    //self.addPurchase = new function(track)
    //{
    //    var r = //g
    //;
}

Album.prototype.matchTrack = function (track) {
    return this.Track() == track.TrackNumber && this.Name().toLowerCase() == track.Album.toLowerCase();
};

// EditPage object
var EditPage = function(data)
{
    var self = this;

    ko.mapping.fromJS(data, albumMapping, this);

    self.nextIndex = function ()
    {
        var max = 1;
        for (var i = 0; i < self.albums().length; i++) {
            var a = self.albums()[i];
            max = Math.max(max, a.Index() + 1);
        }
        return max;
    }
    self.newAlbum = function () {
        var idx = self.nextIndex();
        var temp = ko.mapping.fromJS({ Index: idx, Name: null, Publisher: null, Track: null, PurchaseInfo: null, PurchaseLinks: null }, albumMapping);
        self.albums.push(temp);
    };

    self.removeAlbum = function (a) {
        self.albums.mappedRemove({ Index: a.Index });
    };

    self.promoteAlbum = function (a) {
        var temp = self.albums.mappedRemove({ Index: a.Index });
        self.albums.unshift(temp[0]);
    }

    self.findAlbum = function (track) {
        for (var i = 0; i < viewModel.albums().length; i++) {
            var a = viewModel.albums()[i];
            if (a.matchTrack(track)) {
                return a;
            }
        }

        return null;
    }

    self.chooseTrack = function (track) {
        console.log("Testing");
        var id = track.TrackId;
        var name = track.Album;
        var num = track.TrackNumber;
        console.log("Adding Track:" + id + "(" + name + "#" + num + ")");

        var a = viewModel.findAlbum(track);

        if (a != null) {
            // Merge album
        }
        else {
            var idx = self.nextIndex();
            var temp = new Album({ Index: idx, Name: track.Album, Track: track.TrackNumber, Publisher: "", PurchaseInfo: track.PurchaseInfo, PurchaseLinks: purchaseLinksFromTrack(track) });
            self.albums.push(temp);
        }
    };

}

var albumMapping = {
    'albums': {
        create: function (options) {
            return new Album(options.data);
        },
        key: function (data) {
            return ko.utils.unwrapObservable(data.Index);
        }
    }
}

var pageMapping = {
    create: function(options)
    {
        return new EditPage(options.data);
    }
};


var viewModel = null;

var getServiceInfo = function(service)
{
    var uri = "/api/musicservice/" + songId + "?service=" + service.toString();
    $.getJSON(uri)
        .done(function (data) {
            viewModel.tracks.removeAll();
            var m = { tracks: data };
            viewModel = ko.mapping.fromJS(m, trackMapping, viewModel);
        })
        .fail(function (jqXHR, textStatus, err) {
            console.log(textStatus);
        });
}

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

    $('#load-xbox').click(function () { getServiceInfo('X') });
    $('#load-itunes').click(function () { getServiceInfo('I') });
    $('#load-amazon').click(function () { getServiceInfo('A') });
    $('#load-all').click(function () { getServiceInfo('_') });

    $(document).ajaxSend(function (event, request, settings) {
        $('#loading-indicator').show();
    });

    $(document).ajaxComplete(function (event, request, settings) {
        $('#loading-indicator').hide();
    });


    var data = { tracks: [], albums: albums };
    viewModel = ko.mapping.fromJS(data, pageMapping);


    ko.applyBindings(viewModel);
});



