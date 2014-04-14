$(document).ready(function () {
    var uri = 'api/purchaseinfo';
    $("button").click(function () {
        //window.alert("You clicked me!(" + this.id + ")");
        $.getJSON(uri + '/' + this.id)
            .done(function (data) {
                if (data.Target == null)
                {
                    window.location(data.Link);
                }
                else
                {
                    window.open(data.Link, data.Target)
                }
            })
            .fail(function (jqXHR, textStatus, err) {
                window.alert(err);
                //$('#product').text('Error: ' + err);
            });

    });
});
