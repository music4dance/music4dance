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

//var setupModel = function(tracks)
//{
//    var mapping = {
//        'tracks': {
//            create: function (options) {
//                return new trackModel(options.data);
//            }
//        }
//    };

//    var data = { tracks: tracks };

//    var viewModel = ko.mapping.fromJS(data, mapping);

//    ko.applyBindings(viewModel);
//}

var viewModel = null;

var mapping = {
    'tracks': {
        create: function (options) {
            return new trackModel(options.data);
        }
    }
};

var setupModel = function (tracks)
{
    var data = { tracks: tracks };
    var newModel = ko.mapping.fromJS(data, mapping);

    for (var i = 0; i < newModel.tracks().length; i++)
    {
        viewModel.tracks.push(newModel.tracks()[i]);
    }
}

//var getAllServiceInfo = function()
//{
//    getServiceInfo('X');
//    getServiceInfo('I');
//    getServiceInfo('A');
//}

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


    var data = { tracks: [] };
    viewModel = ko.mapping.fromJS(data);

    ko.applyBindings(viewModel);
});



//var data = {
//    tracks: [
//       {
//           "Service": 3,
//           "TrackId": "music.0A8B6E07-0100-11DB-89CA-0019B92A3933",
//           "Name": "Trust",
//           "CollectionId": null,
//           "AltId": null,
//           "Artist": "Neon Trees",
//           "Album": "Picture Show",
//           "ImageUrl": "http://musicimage.xboxlive.com/content/music.0A8B6E07-0100-11DB-89CA-0019B92A3933/image?locale=en-US",
//           "Link": "http://music.xbox.com/Track/0A8B6E07-0100-11DB-89CA-0019B92A3933?partnerID=music4dance?action=play&target=app",
//           "ReleaseDate": "2012-04-17T00:00:00Z",
//           "Genre": "Rock",
//           "Duration": 365,
//           "TrackNumber": 7
//       },
//       {
//           "Service": 3,
//           "TrackId": "music.9EC13708-0100-11DB-89CA-0019B92A3933",
//           "Name": "I Love You (But I Hate Your Friends)",
//           "CollectionId": null,
//           "AltId": null,
//           "Artist": "Neon Trees",
//           "Album": "I Love You (But I Hate Your Friends)",
//           "ImageUrl": "http://musicimage.xboxlive.com/content/music.9EC13708-0100-11DB-89CA-0019B92A3933/image?locale=en-US",
//           "Link": "http://music.xbox.com/Track/9EC13708-0100-11DB-89CA-0019B92A3933?partnerID=music4dance?action=play&target=app",
//           "ReleaseDate": "2014-03-13T00:00:00Z",
//           "Genre": "Rock",
//           "Duration": 196,
//           "TrackNumber": 1
//       },
//       {
//           "Service": 3,
//           "TrackId": "music.DACB2B07-0100-11DB-89CA-0019B92A3933",
//           "Name": "Moving In The Dark",
//           "CollectionId": null,
//           "AltId": null,
//           "Artist": "Neon Trees",
//           "Album": "Picture Show",
//           "ImageUrl": "http://musicimage.xboxlive.com/content/music.DACB2B07-0100-11DB-89CA-0019B92A3933/image?locale=en-US",
//           "Link": "http://music.xbox.com/Track/DACB2B07-0100-11DB-89CA-0019B92A3933?partnerID=music4dance?action=play&target=app",
//           "ReleaseDate": "2012-04-04T00:00:00Z",
//           "Genre": "Rock",
//           "Duration": 182,
//           "TrackNumber": 1
//       },
//       {
//           "Service": 3,
//           "TrackId": "music.128B6E07-0100-11DB-89CA-0019B92A3933",
//           "Name": "Don't You Want Me",
//           "CollectionId": null,
//           "AltId": null,
//           "Artist": "Neon Trees",
//           "Album": "Picture Show",
//           "ImageUrl": "http://musicimage.xboxlive.com/content/music.128B6E07-0100-11DB-89CA-0019B92A3933/image?locale=en-US",
//           "Link": "http://music.xbox.com/Track/128B6E07-0100-11DB-89CA-0019B92A3933?partnerID=music4dance?action=play&target=app",
//           "ReleaseDate": "2012-04-17T00:00:00Z",
//           "Genre": "Rock",
//           "Duration": 337,
//           "TrackNumber": 15
//       },
//       {
//           "Service": 3,
//           "TrackId": "music.048B6E07-0100-11DB-89CA-0019B92A3933",
//           "Name": "Moving In The Dark",
//           "CollectionId": null,
//           "AltId": null,
//           "Artist": "Neon Trees",
//           "Album": "Picture Show",
//           "ImageUrl": "http://musicimage.xboxlive.com/content/music.048B6E07-0100-11DB-89CA-0019B92A3933/image?locale=en-US",
//           "Link": "http://music.xbox.com/Track/048B6E07-0100-11DB-89CA-0019B92A3933?partnerID=music4dance?action=play&target=app",
//           "ReleaseDate": "2012-04-17T00:00:00Z",
//           "Genre": "Rock",
//           "Duration": 182,
//           "TrackNumber": 1
//       },
//       {
//           "Service": 3,
//           "TrackId": "music.D15B4608-0100-11DB-89CA-0019B92A3933",
//           "Name": "I Love You (But I Hate Your Friends)",
//           "CollectionId": null,
//           "AltId": null,
//           "Artist": "Neon Trees",
//           "Album": "Pop Psychology",
//           "ImageUrl": "http://musicimage.xboxlive.com/content/music.D15B4608-0100-11DB-89CA-0019B92A3933/image?locale=en-US",
//           "Link": "http://music.xbox.com/Track/D15B4608-0100-11DB-89CA-0019B92A3933?partnerID=music4dance?action=play&target=app",
//           "ReleaseDate": "2014-04-10T00:00:00Z",
//           "Genre": "Rock",
//           "Duration": 196,
//           "TrackNumber": 5
//       },
//       {
//           "Service": 3,
//           "TrackId": "music.E6CB2B07-0100-11DB-89CA-0019B92A3933",
//           "Name": "Tell Me You Love Me",
//           "CollectionId": null,
//           "AltId": null,
//           "Artist": "Neon Trees",
//           "Album": "Picture Show",
//           "ImageUrl": "http://musicimage.xboxlive.com/content/music.E6CB2B07-0100-11DB-89CA-0019B92A3933/image?locale=en-US",
//           "Link": "http://music.xbox.com/Track/E6CB2B07-0100-11DB-89CA-0019B92A3933?partnerID=music4dance?action=play&target=app",
//           "ReleaseDate": "2012-04-04T00:00:00Z",
//           "Genre": "Rock",
//           "Duration": 238,
//           "TrackNumber": 13
//       },
//       {
//           "Service": 3,
//           "TrackId": "music.0D3A3B08-0100-11DB-89CA-0019B92A3933",
//           "Name": "Voices In The Halls",
//           "CollectionId": null,
//           "AltId": null,
//           "Artist": "Neon Trees",
//           "Album": "Voices In The Halls",
//           "ImageUrl": "http://musicimage.xboxlive.com/content/music.0D3A3B08-0100-11DB-89CA-0019B92A3933/image?locale=en-US",
//           "Link": "http://music.xbox.com/Track/0D3A3B08-0100-11DB-89CA-0019B92A3933?partnerID=music4dance?action=play&target=app",
//           "ReleaseDate": "2014-03-20T00:00:00Z",
//           "Genre": "Rock",
//           "Duration": 179,
//           "TrackNumber": 1
//       },
//       {
//           "Service": 3,
//           "TrackId": "music.E7CB2B07-0100-11DB-89CA-0019B92A3933",
//           "Name": "Take Me For A Ride",
//           "CollectionId": null,
//           "AltId": null,
//           "Artist": "Neon Trees",
//           "Album": "Picture Show",
//           "ImageUrl": "http://musicimage.xboxlive.com/content/music.E7CB2B07-0100-11DB-89CA-0019B92A3933/image?locale=en-US",
//           "Link": "http://music.xbox.com/Track/E7CB2B07-0100-11DB-89CA-0019B92A3933?partnerID=music4dance?action=play&target=app",
//           "ReleaseDate": "2012-04-04T00:00:00Z",
//           "Genre": "Rock",
//           "Duration": 338,
//           "TrackNumber": 14
//       },
//       {
//           "Service": 3,
//           "TrackId": "music.098B6E07-0100-11DB-89CA-0019B92A3933",
//           "Name": "Lessons In Love (All Day, All Night)",
//           "CollectionId": null,
//           "AltId": null,
//           "Artist": "Neon Trees",
//           "Album": "Picture Show",
//           "ImageUrl": "http://musicimage.xboxlive.com/content/music.098B6E07-0100-11DB-89CA-0019B92A3933/image?locale=en-US",
//           "Link": "http://music.xbox.com/Track/098B6E07-0100-11DB-89CA-0019B92A3933?partnerID=music4dance?action=play&target=app",
//           "ReleaseDate": "2012-04-17T00:00:00Z",
//           "Genre": "Rock",
//           "Duration": 223,
//           "TrackNumber": 6
//       }
//    ]
//};
