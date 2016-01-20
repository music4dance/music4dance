$(document).ready(function() {
    // Handling for purchase links
    var uri = '/api/purchaseinfo/';
    $('.play-link').click(function() {
        //window.alert("You clicked me!(" + this.id + ")");

        $.getJSON(uri + this.id[0] + '?songs=' + this.id.substring(1))
            .done(function(data) {
                //if (data[0].Target == null) {
                    window.location = data[0].Link;
                //} else {
                //    window.open(data[0].Link, data[0].Target);
                //}
            })
            .fail(function() {
                window.alert('Unable to fetch data from ' + this.id + 'please try again later.  If this issue persists, please report it to us at info@music4dance.net');
                //$('#product').text('Error: ' + err);
            });
    });

    var currentDance = null;
    $('[data-toggle="popover"]').popover({ html: true, trigger: 'click' });
    $('[data-toggle="popover"]').on('show.bs.popover', function () {
        if (currentDance && currentDance !== this) {
            $(currentDance).queue(function (next) {
                $(this).popover('hide');
                next();
            });
        }
        currentDance = this;
    });

    // Handle like links
    $('.toggle-like').click(function (event) {
        event.preventDefault();
        //window.alert("You clicked me!(" + this.id + ")");
        var $this = $(this);
        var fields = $this.attr('id').split('.');
        var like = $this.data('like');
        switch (like) {
            default:
                like = true;
                break;
            case true:
                like = false;
                break;
            case false:
                like = null;
                break;
        }

        var dance = '';
        var type = 'heart';
        var tipText = 'this song';
        if (fields[0] !== 'like') {
            dance = fields[0];
            type = 'dance';
            tipText = 'dancing to this song';
        }
        var t = '/api/updatelike/' + fields[1] + '?dance=' + dance + '&like=' + like;
        $.getJSON(t)
            .done(function () {
                var img = $this.find('img');
                var str = (like === null) ? 'null' : (like ? 'true' : 'false');
                var voteOpts = window.VoteOptions[str];

                $this.data('like', like);
                $this.attr('title', voteOpts.tip.replace('{0}',tipText));
                img.prop('src', '/content/' + type + voteOpts.img + '-icon.png');
            })
            .fail(function (jqxhr, textStatus, err) {
                window.alert(err);
                //$('#product').text('Error: ' + err);
            });
    });

    // Handle the spotify control
    var spotify = $('#spotify-player');
    if (!spotify) return;

    var name = spotify.attr('data-trackset-name');
    var ids = spotify.attr('data-trackset-songs');

    if (!name || !ids) return;
    var button = spotify.children('button');

    button.click(function () {
        button.replaceWith('<span>Loading Spotify</span>');
        button = spotify.children('span');
        var listUrl = uri + 's?songs=' + ids + '&fulllink=false'; //.substring(0,16);
        $.getJSON(listUrl)
            .done(function(data) {
                var player = '<iframe  src="https://embed.spotify.com/?uri=spotify:trackset:' + name + ':' + data + '" frameborder="0" allowtransparency="true"></iframe>';
                button.replaceWith(player);
            })
            .fail(function () {
                button.replaceWith('<span>No Spotify Tracks on this page.</span>');
                //window.alert(2err);
                //$('#product').text('Error: ' + err);
            });
    });
});
