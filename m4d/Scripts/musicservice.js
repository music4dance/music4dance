$(document).ready(function () {
    var purchaseUri = '/api/purchaseinfo/';

    // setup the viewmodel for modals
    var viewModel = {
        songId: ko.observable(''),
        title: ko.observable(''),
        artist: ko.observable(''),
        purchase: ko.observableArray(),
        sample: ko.observable(''),
        tagValue: ko.observable(''),
        danceName: ko.observable(''),
        url: ko.observable(''),
        filteredUrl: ko.observable(''),
        danceUrl: ko.observable(''),
        tagText: ko.observable('')
    };

    ko.applyBindings(viewModel);

    // Setup tool-tips
    $('[data-toggle="tooltip"]').tooltip();

    // Handling for the player
    var purchaseInfo = {
        A: { id: 'A', logo: 'amazon', title: 'Amazon', help: 'Available at Amazon' },
        I: { id: 'I', logo: 'itunes', title: 'ITunes', help: 'Available on ITunes' },
        S: { id: 'S', logo: 'spotify', title: 'Spotify', help: 'Available on Spotify' },
        X: { id: 'X', logo: 'xbox', title: 'Groove', help: 'Available on Groove Music' }
    };

    var setupModal = function(event)
    {
        var t = $(event.relatedTarget);
        viewModel.artist(t.data('artist'));
        viewModel.title(t.data('title'));
        return t;
    }

    $('#playModal').on('show.bs.modal', function (event) {
        var t = setupModal(event);
        viewModel.songId(t.data('song-id'));
        var s = t.data('sample');
        if (s === '.') s = null;
        viewModel.sample(s);

        viewModel.purchase.removeAll();
        var p = t.data('purchase');

        for (var i = 0; i < p.length; i++) {
            var info = purchaseInfo[p[i]];
            if (info) {
                viewModel.purchase.push(info);
            }
        }

        if (s !== null) {
            $('#sample-player').attr('src',s).trigger('play');
        }

        // Handling for purchase links
        $('.play-link').click(function () {
            $.getJSON(purchaseUri + this.id[0] + '?songs=' + this.id.substring(1))
                .done(function (data) {
                    //if (data[0].Target == null) {
                    window.location = data[0].Link;
                    //} else {
                    //    window.open(data[0].Link, data[0].Target);
                    //}
                })
                .fail(function () {
                    window.alert('Unable to fetch data from ' + this.id + 'please try again later.  If this issue persists, please report it to us at info@music4dance.net');
                    //$('#product').text('Error: ' + err);
                });
        });
    });

    $('#playModal').on('hide.bs.modal', function () {
        var s = $('#sample-player');
        if (s.length) {
            s.trigger('pause');
        }
    });

    var setupTagModal = function(event) {
        var t = setupModal(event);

        viewModel.danceName(t.data('dance-name'));
        viewModel.url(t.data('url'));
        viewModel.filteredUrl(t.data('filtered-url'));
        viewModel.danceUrl(t.data('dance-url'));

        return t;
    }

    // Handling for tag filter
    $('#filterModal').on('show.bs.modal', function (event) {
        $('#danceModal').modal('hide');
        var t = setupTagModal(event);
        viewModel.tagValue(t.data('tag-value'));
    });

    // Handling for dance filter
    $('#danceModal').on('show.bs.modal', function (event) {
        var t = setupTagModal(event);
        viewModel.tagText(t.data('tags'));
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
        var listUrl = purchaseUri + 's?songs=' + ids + '&fulllink=false'; //.substring(0,16);
        $.getJSON(listUrl)
            .done(function(data) {
                var player = '<iframe  src="https://open.spotify.com/embed?uri=spotify:trackset:' + name + ':' + data + '" frameborder="0" allowtransparency="true" allow="encrypted-media"></iframe>';
                button.replaceWith(player);
            })
            .fail(function () {
                button.replaceWith('<span>No Spotify Tracks on this page.</span>');
                //window.alert(2err);
                //$('#product').text('Error: ' + err);
            });
    });
});
