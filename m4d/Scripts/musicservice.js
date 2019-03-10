$(document).ready(function () {
    var purchaseUri = '/api/purchaseinfo/';

    var MusicService = function() {
        var self = this;

        self.songId = ko.observable('');
        self.title = ko.observable('');
        self.artist = ko.observable('');
        self.purchase = ko.observableArray();
        self.sample = ko.observable('');
        self.tagValue = ko.observable('');
        self.danceId = ko.observable('');
        self.danceName = ko.observable('');
        self.url = ko.observable('');
        self.filteredUrl = ko.observable('');
        self.danceUrl = ko.observable('');
        self.tagText = ko.observable('');
        self.danceLike = ko.observable(null);
        self.danceWeight = ko.observable(0);
        self.danceWeightMax = ko.observable(0);

        self.likeUrl = ko.pureComputed(function() {
            var like = self.danceLike();
            var type = like === null ? 'outline-' : like === false ? 'broken-' : '';
            return '/Content/dance-' + type + 'icon.png';
        });
        self.likeNullText = ko.pureComputed(function() {
            return 'Click to like/dislike dancing ' + self.danceName() + ' to ' + self.title();
        });
        self.likePosText = ko.pureComputed(function () {
            return 'You have liked dancing ' + self.danceName() + ' to ' + self.title() + '. Click to dislike.';
        });
        self.likeNegText = ko.pureComputed(function () {
            return 'You have disliked dancing ' + self.danceName() + ' to ' + self.title() + '. Click to reset your vote.';
        });

        self.likeText = ko.pureComputed(function () {
            if (!window.isAuthenticated) {
                return 'Sign in to vote';
            }
            var like = self.danceLike();
            return like === null ? self.likeNullText() : like === false ? self.likeNegText() : self.likePosText();
        });
    };

    var viewModel = new MusicService();

    ko.applyBindings(viewModel);

    var rotateLike = function(like) {
        switch (like) {
        default:
            return true;
        case true:
            return false;
        case false:
            return null;
        }
    };

    // Setup tool-tips
    $('[data-toggle="tooltip"]').tooltip();

    // Handling for the player
    var purchaseInfo = {
        A: { id: 'A', logo: 'amazon', title: 'Amazon', help: 'Available at Amazon' },
        I: { id: 'I', logo: 'itunes', title: 'ITunes', help: 'Available on ITunes' },
        S: { id: 'S', logo: 'spotify', title: 'Spotify', help: 'Available on Spotify' }
    };

    var setupModal = function(event) {
        var t = $(event.relatedTarget);
        viewModel.artist(t.data('artist'));
        viewModel.title(t.data('title'));
        return t;
    };

    $('#playModal').on('show.bs.modal', function (event) {
        var t = setupModal(event);
        viewModel.songId(t.data('song-id'));
        var s = t.data('sample');
        if (s === '.') s = null;
        viewModel.sample(s);

        viewModel.purchase.removeAll();
        var p = t.data('purchase');

        for (var i = 0; i < p.length; i++) {
            var info = purchaseInfo[p[i].Id];
            if (info) {
                p[i].Title = info.title;
                p[i].Help = info.help;
                viewModel.purchase.push(p[i]);
            }
        }

        if (s !== null) {
            $('#sample-player').attr('src',s).trigger('play');
        }
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
    };

    // Handling for tag filter
    $('#filterModal').on('show.bs.modal', function (event) {
        $('#danceModal').modal('hide');
        var t = setupTagModal(event);
        viewModel.tagValue(t.data('tag-value'));
    });

    // Handling for dance filter
    $('#danceModal').on('show.bs.modal', function (event) {
        var t = setupTagModal(event);
        viewModel.songId(t.data('song-id'));
        viewModel.danceId(t.data('dance-id'));
        viewModel.tagText(t.data('tags'));
        viewModel.danceWeight(t.data('dance-weight'));
        viewModel.danceWeightMax(t.data('dance-max-weight'));
        if (window.isAuthenticated) {
            $.ajax({
                    method: 'GET',
                    url: '/api/like/' + viewModel.songId(),
                    data: { 'dance': viewModel.danceId() }
                })
                .done(function (data) {
                    viewModel.danceLike(data.like);
                    //window.alert(data.like);
                })
                .fail(function (jqxhr, textStatus, err) {
                    window.alert(err);
                    //$('#product').text('Error: ' + err);
                });

            viewModel.danceLike();
        }
    });

    var updateLike = function (item, like, type) {
        var tipText = 'this song';
        if (type !== 'heart') {
            tipText = 'dancing to this song';
        }

        var img = item.find('img');
        var str = like === null ? 'null' : like ? 'true' : 'false';
        var voteOpts = window.VoteOptions[str];

        var modalId = '#' + item.attr('id') + '.modal';
        var modal = $(modalId.replace(/\./g,'\\.'));
        var weight = modal.data('dance-weight');
        var maxWeight = modal.data('dance-max-weight');

        switch (like) {
            case true:
                weight += 2;
                break;
            case false:
                if (weight === maxWeight) {
                    maxWeight = weight;
                }
                weight -= 3;
                break;
            default:
                weight += 1;
                break;
        }

        if (maxWeight < weight) {
            maxWeight = weight;
        }

        modal.data('dance-weight', weight);
        modal.data('dance-max-weight', maxWeight);
        item.data('like', like);
        item.attr('title', voteOpts.tip.replace('{0}', tipText));
        img.prop('src', '/content/' + type + voteOpts.img + '-icon.png');
    };

    // Handle dance modal likes
    $('#dance-modal-like').click(function(event) {
        event.preventDefault();

        if (!window.isAuthenticated) {
            window.location.href = '/account/signin?returnUrl=' + window.location.href;
            return;
        }

        var like = rotateLike(viewModel.danceLike());

        $.ajax({
                method: 'PUT',
                url: '/api/like/' + viewModel.songId(),
                data: { 'dance': viewModel.danceId(), 'like': like }
            })
            .done(function () {
                viewModel.danceLike(like);
                var id = '#' + viewModel.danceId() + '\\.' + viewModel.songId();
                var item = $(id);
                updateLike(item, like, 'dance');
                var modalId = '#' + item.attr('id') + '.modal';
                var modal = $(modalId.replace(/\./g, '\\.'));
                viewModel.danceWeight(modal.data('dance-weight'));
                viewModel.danceWeightMax(modal.data('dance-max-weight'));
            })
            .fail(function (jqxhr, textStatus, err) {
                window.alert(err);
                //$('#product').text('Error: ' + err);
            });
    });

    // Handle like links
    $('.toggle-like').click(function (event) {
        event.preventDefault();
        //window.alert("You clicked me!(" + this.id + ")");
        var $this = $(this);
        var fields = $this.attr('id').split('.');
        var like = rotateLike($this.data('like'));

        var dance = '';
        var type = 'heart';
        if (fields[0] !== 'like') {
            dance = fields[0];
            type = 'dance';
        }

        $.ajax({
            method: 'PUT',
            url: '/api/like/' + fields[1],
            data: {'dance': dance, 'like' : like}
            })
            .done(function () {
                updateLike($this, like, type);
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
                //var player = '<iframe  src="https://open.spotify.com/embed?uri=spotify:trackset:' + name + ':' + data + '" frameborder="0" allowtransparency="true" allow="encrypted-media"></iframe>';
                var player = '<span>Spotify has deprecated the feature that we were using to display the player, we have a partial solution in place, but have not found a solution that covers this search.  More details are available <a href="https://www.music4dance.net//blog/playing-songs-from-music4dance/">here</a>.</span>';
                button.replaceWith(player);
            })
            .fail(function () {
                button.replaceWith('<span>No Spotify Tracks on this page.</span>');
                //window.alert(2err);
                //$('#product').text('Error: ' + err);
            });
    });
});
