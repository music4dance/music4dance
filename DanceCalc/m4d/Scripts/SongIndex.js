
$(document).ready(function () {
    // Handling for show/hide advanced search
    $("#ToggleAdvanced").click(function () {
        if (showAdvanced)
        {
            ShowBasic();
        }
        else
        {
            ShowAdvanced();
        }
        showAdvanced = !showAdvanced;
    });

    if (showAdvanced)
    {
        ShowAdvanced();
    }
    else
    {
        ShowBasic();
    }

    // Handling for Dance selector
    $('.search-panel .dropdown-menu').find('a').click(function (e) {
        e.preventDefault();
        var param = $(this).attr("href").replace("#", "");
        var concept = $(this).text();
        $('.search-panel span#dance_selector').text(concept);

        var dances = $('.input-group #dances');
        dances.val(param);

        $("#submit-search").click();
    });
});

function ShowAdvanced()
{
    var button = '<span id="left-icon" class="glyphicon glyphicon-arrow-up"></span> Less <span id="right-icon" class="glyphicon glyphicon-arrow-up"></span>';

    $("#AdvancedSearch").show();
    var search = $("#search");
    search.attr("action", "/Song/AdvancedSearch");
    $("#ToggleAdvanced").html(button);
}

function ShowBasic()
{
    var button = '<span id="left-icon" class="glyphicon glyphicon-arrow-down"></span> More <span id="right-icon" class="glyphicon glyphicon-arrow-down"></span>';

    $("#AdvancedSearch").hide();
    $("#search").attr("action", "/Song/Search");
    $("#ToggleAdvanced").html(button);
}

