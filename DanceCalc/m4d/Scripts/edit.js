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

var addPurchaseLink = function(link,olist)
{
    if (link != null)
    {
        // TODO: Has to be a cleaner way to find existence ($.inArray isn't working
        //  possibly because we're mapped - may be that going back and figuring
        //  out how to get KO Mapping to mapp the PurchaseLink array but not
        //  the objects is the way to go...

        for (var i = 0; i < olist().length; i++) {
            if (link.Link == olist()[i].Link())
            {
                return; // Already have this link
            }
        }

        var olink = ko.mapping.fromJS(link);
        if ($.inArray(olink, olist()) == -1)
        {
            olist.push(olink);
        }
    }
}

// Album object
var Album = function (data) {
    var self = this;

    //{'copy': "PurchaseLinks" }
    ko.mapping.fromJS(data, {}, this);

    self.addPurchase = function(track)
    {
        //var pi = self.PurchasInfo();
        //var r = /[AIX][SA]=[^;]*/g, match;

        //while (match)

        // First do the string based purchase info
        var pi = self.PurchaseInfo();

        var tpi = track.PurchaseInfo;
        if (pi == null)
        {
            self.PurchaseInfo(tpi);
        }
        else if (tpi != null)
        {
            // get rid of possible terminal ;
            if (pi[pi.length-1] == ";")
            {
                pi = pi.substring(0,pi.length-1);
            }

            // split up the new purchase info
            tpis = tpi.split(";");

            for (var i = 0; i < tpis.length; i++)
            {
                if (pi.indexOf(tpis[i]) == -1)
                {
                    pi += ";" + tpis[i];
                }
            }

            self.PurchaseInfo(pi);
        }

        // Then add in the purchase links
        addPurchaseLink(track.SongLink, self.PurchaseLinks);
        //addPurchaseLink(track.AlbumLink, self.PurchaseLinks());
    };
}

Album.prototype.matchTrack = function (track)
{
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

    self.removeAlbum = function (data, event) {
        self.albums.mappedRemove({ Index: data.Index });
        event.preventDefault();
    };

    self.promoteAlbum = function (data,event) {
        var temp = self.albums.mappedRemove({ Index: data.Index });
        self.albums.unshift(temp[0]);
        event.preventDefault();
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

    self.chooseTrack = function (track,event) {
        event.preventDefault();
        console.log("Testing");
        var id = track.TrackId;
        var name = track.Album;
        var num = track.TrackNumber;
        console.log("Adding Track:" + id + "(" + name + "#" + num + ")");

        var a = viewModel.findAlbum(track);

        if (a != null) {
            a.addPurchase(track);
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



