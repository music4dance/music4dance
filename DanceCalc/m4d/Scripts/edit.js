function deleteAlbum(event) {
    var id = "#Album_" + event.data.toString();
    var base = "#Albums_" + event.data.toString();
    var name = base + "__Name";
    var track = base + "__Track";
    var publisher = base + "__Publisher";

    $(id).hide();

    $(name).val(null);
    $(track).val(null);
    $(publisher).val(null);
}

function addAlbum() {
    var name = "#Album_" + (albumCount + albumsAdded).toString();

    albumsAdded += 1;
    $(name).show();

    if (albumsAdded == 4) {
        $('#AddAlbum').hide();
    }
}

function danceAction(id) {
    Debug.write("Dance Id=" + id + "\r\n");
    $('#addDances option[value="' + id + '"]').attr('selected', 'selected');
    $('#addDances').trigger('chosen:updated');

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

var trackModel = function(data)
{
    ko.mapping.fromJS(data, {}, this);

    this.durationFormatted = ko.computed(function () {
        return formatDuration(this.Duration());
    }, this);

    this.serviceLogo = ko.computed(function () {
        return logoFromEnum(this.Service());
    }, this);
}

var albumModel = function (data) {
    ko.mapping.fromJS(data, {}, this);

    // TODONEXT: Figure out how to get live album manipulation working and
    //  verify that editing of album info back to the database still
    //  works
    // TODO: Computed properties?
}

var computeAlbumName = function(idx,name)
{
    var ret = "Albums[" + idx + "]." + name;
    return ret;
}

var computeAlbumId = function (idx, name) {
    var ret = "Albums_" + idx + "__" + name;
    return ret;
}

var viewModel = null;

var trackMapping = {
    'tracks': {
        create: function (options) {
            return new trackModel(options.data);
        }
    }
};

var setupModel = function (tracks)
{
    viewModel.tracks.removeAll();
    var data = { tracks: tracks };
    viewModel= ko.mapping.fromJS(data, trackMapping, viewModel);
}

var getServiceInfo = function(service)
{
    var uri = "/api/musicservice/" + songId + "?service=" + service.toString();
    $.getJSON(uri)
        .done(function (data) {
            setupModel(data);
        })
        .fail(function (jqXHR, textStatus, err) {
            console.log(textStatus);
        });
}

$(document).ready(function () {
    for (var i = 0; i < albumCount; i++)
    {
        var name = "#Delete_" + i.toString();
        $(name).click(i,deleteAlbum)
    }

    $('#AddAlbum').click(addAlbum);

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


    var albumMapping = {
        'albums': {
            create: function (options) {
                return new albumModel(options.data);
            },
            key: function(data){
                return ko.utils.unwrapObservable(data.Index);
            }
        }

    };

    var data = { tracks: [], albums: albums };
    viewModel = ko.mapping.fromJS(data, albumMapping);
    viewModel.chooseTrack = function (track)
    {
        console.log("Adding Track:" + track.trackId() + "(" + track.Album() + "#" + track.TrackNumber() + ")");
    };
    viewModel.removeAlbum = function (album)
    {
        viewModel.albums.mappedRemove({ Index: album.Index });
        console.log("Remove Album:" + album.Name() + "(" + album.Index() + ")")
    };

    ko.applyBindings(viewModel);
});



