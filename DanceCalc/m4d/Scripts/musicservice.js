$(document).ready(function() {
    // Handling for purchase links
    var uri = '/api/purchaseinfo/';
    $(".play-link").click(function() {
        //window.alert("You clicked me!(" + this.id + ")");

        $.getJSON(uri + this.id)
            .done(function(data) {
                if (data.Target == null) {
                    window.location(data.Link);
                } else {
                    window.open(data.Link, data.Target);
                }
            })
            .fail(function(jqXHR, textStatus, err) {
                window.alert(err);
                //$('#product').text('Error: ' + err);
            });
    });

    $('[data-toggle="popover"]').popover({html:true, trigger:'click'});
});
