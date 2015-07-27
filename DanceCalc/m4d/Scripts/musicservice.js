$(document).ready(function() {
    // Handling for purchase links
    var uri = '/api/purchaseinfo/';
    $(".play-link").click(function() {
        //window.alert("You clicked me!(" + this.id + ")");

        $.getJSON(uri + this.id[0] + '?songs=' + this.id.substring(1))
            .done(function(data) {
                if (data[0].Target == null) {
                    window.location(data[0].Link);
                } else {
                    window.open(data[0].Link, data[0].Target);
                }
            })
            .fail(function(jqXHR, textStatus, err) {
                window.alert(err);
                //$('#product').text('Error: ' + err);
            });
    });

    $('[data-toggle="popover"]').popover({ html: true, trigger: 'click' });

    // Handle the spotify control
    var spotify = $("#spotify-player");
    if (!spotify) return;

    var name = spotify.attr("data-trackset-name");
    var ids = spotify.attr("data-trackset-songs");

    if (!name || !ids) return;

    var listUrl = uri + "s?songs=" + ids + "&fulllink=false";//.substring(0,16);
    $.getJSON(listUrl)
        .done(function (data) {
            var player = '<iframe  src="https://embed.spotify.com/?uri=spotify:trackset:' + name + ':' + data + '" frameborder="0" allowtransparency="true"></iframe>';
            spotify.append(player);
        })
        .fail(function (jqXHR, textStatus, err) {
            window.alert(err);
            //$('#product').text('Error: ' + err);
        });
});
