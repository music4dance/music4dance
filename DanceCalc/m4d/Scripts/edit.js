var danceAction = function (id) {
    $('#addDances option[value="' + id + '"]').attr('selected', 'selected');
    $('#addDances').trigger('chosen:updated');
};

/// Helper functions

var computeAlbumName = function (idx, name) {
    var ret = "Albums[" + idx + "]." + name;
    return ret;
};

var computeAlbumId = function (idx, name) {
    var ret = "Albums_" + idx + "__" + name;
    return ret;
};

var formatDuration = function (seconds) {
    var m = Math.floor(seconds / 60);
    var s = seconds % 60;
    var sec = (s < 10) ? ("0" + s.toString()) : s.toString();

    return m.toString() + ":" + sec;
};

var logoFromEnum = function (e) {
    var ret = null;
    switch (e) {
        case 1: ret = "/Content/amazon-logo.png";
            break;
        case 2: ret = "/Content/itunes-logo.png";
            break;
        case 3: ret = "/Content/xbox-logo.png";
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
    var field = $("#" + altToField(id));
    var text = self.text();
    self.text(field.val());
    field.val(text);
    //alert(self.context.parentElement.id + ":" + self.text());
};

var addValue = function (id, val) {
    if (val == null) {
        return;
    }

    var self = $("#" + id);
    var field = $("#" + altToField(id));

    var oldVal = field.val();
    if (val === oldVal) {
        return;
    }

    var dup = false;
    $("#" + id + " a").each(function () {
        if ($(this).text() == val) {
            dup = true;
            return false;
        }
    });

    if (!dup) {
        var node = "<a href='#' role='button' class='btn btn-link'>" + oldVal + "</a>"
        self.append(node);
        field.val(val);
    }
};

// Track object
var Track = function (data) {
    //var mapping = {};//{'observe': ["Name"]};
    //ko.mapping.fromJS(data, mapping, this);
    $.extend(true, this, data);

    this.durationFormatted = ko.computed(function () {
        return formatDuration(this.Duration);
    }, this);

    this.serviceLogo = ko.computed(function () {
        return logoFromEnum(this.Service);
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
            if (link.Link == olist()[i].Link()) {
                return; // Already have this link
            }
        }

        var olink = ko.mapping.fromJS(link);
        if ($.inArray(olink, olist()) == -1) {
            olist.push(olink);
        }
    }
};

// Album object
var Album = function (data) {
    var self = this;

    //{'copy': "PurchaseLinks" }
    ko.mapping.fromJS(data, {}, this);

    self.addPurchase = function (track) {
        //var pi = self.PurchasInfo();
        //var r = /[AIX][SA]=[^;]*/g, match;

        //while (match)

        // First do the string based purchase info
        var pi = self.PurchaseInfo();

        var tpi = track.PurchaseInfo;
        if (pi == null) {
            self.PurchaseInfo(tpi);
        }
        else if (tpi != null) {
            // get rid of possible terminal ;
            if (pi[pi.length - 1] == ";") {
                pi = pi.substring(0, pi.length - 1);
            }

            // split up the new purchase info
            tpis = tpi.split(";");

            for (var i = 0; i < tpis.length; i++) {
                if (pi.indexOf(tpis[i]) == -1) {
                    pi += ";" + tpis[i];
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
    return (this.Track() == null || this.Track() == track.TrackNumber) && this.Name().toLowerCase() == track.Album.toLowerCase();
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

    self.promoteAlbum = function (data, event) {
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

        // Update the track info
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

        // Now add in the extra top level info if empty
        //  or replace and put in a change back button if not

        addValue('alt-title', track.Name);
        addValue('alt-artist', track.Artist);
        addValue('alt-length', track.durationFormatted());
        addValue('alt-tempo', track.Tempo);

        // Finally handle genre
        if (track.Genre != null)
        {
            var gnew = track.Genre + ":Music";
            var gval = $("#editTags").val();
            if (gval == null)
            {
                gval = "";
            }
            var glist = gval.toLowerCase().split("|");

            if (glist.indexOf(gnew.toLowerCase()) == -1)
            {
                if (gval.length > 0)
                {
                    gval += "|";
                }
                gval += gnew;
            }

            $("#editTags").val(gval);
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
};

var pageMapping = {
    create: function(options)
    {
        return new EditPage(options.data);
    }
};


var viewModel = null;

var getServiceInfo = function(service)
{
    var uri = "/api/musicservice/" + songId + "?service=" + service.toString() + "&Title=";
    var t = $('#search').val();
    if (t.length > 0)
    {
        uri += encodeURI(t);
    }
    else
    {
        uri += encodeURI($('#Title').val()) + "&Artist=" + encodeURI($('#Artist').val());
    }

    aid = "#" + computeAlbumId(0,"Name");
    afield = $(aid);
    if (afield.length)
    {
        uri += "&Album=" + afield.val();
    }

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

    $('#load-xbox').click(function () { getServiceInfo('X'); });
    $('#load-itunes').click(function () { getServiceInfo('I'); });
    $('#load-amazon').click(function () { getServiceInfo('A'); });
    $('#load-all').click(function () { getServiceInfo('_'); });

    //$('#alt-title a').on("click", function (event) {
    //    event.preventDefault();
    //    replaceValue($(this));
    //});

    $('#alt-title').on("click", ".btn", null, function (event) {
        event.preventDefault();
        replaceValue($(this));
    });
    $('#alt-artist').on("click", ".btn", null, function (event) {
        event.preventDefault();
        replaceValue($(this));
    });
    $('#alt-length').on("click", ".btn", null, function (event) {
        event.preventDefault();
        replaceValue($(this));
    });
    $('#alt-tempo').on("click", ".btn", null, function (event) {
        event.preventDefault();
        replaceValue($(this));
    });

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
