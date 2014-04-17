$(document).ready(function () {
    var uri = '/api/purchaseinfo/';
    $("td > button").click(function () {
        //window.alert("You clicked me!(" + this.id + ")");
        $.getJSON(uri +  this.id)
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
    $("#ShowAdvanced").click(function () {
        $("#AdvancedSearch").show();
        $("#BasicSearch").hide();
    });
    $("#ShowBasic").click(function () {
        $("#AdvancedSearch").hide();
        $("#BasicSearch").show();
    });

    if (showAdvanced)
    {
        $("#AdvancedSearch").show();
        $("#BasicSearch").hide();
    }
});
