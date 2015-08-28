
$(document).ready(function () {
    // Handling for show/hide advanced search
    $('#ToggleAdvanced').click(function () {
        if (showAdvanced)
        {
            ShowBasic();
        }
        else
        {
            ShowAdvanced();
        }
        window.showAdvanced = !window.showAdvanced;
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
        var param = $(this).attr('href').replace('#', '');
        var name = $(this).text();
        $('.search-panel span#dance_selector').text(name);

        var dances = $('.input-group #dances');
        dances.val(param);

        var submit = true;
        if (param === 'ALL') {
            $('#chosen-dances').val([]);
            $('#dances').val('');
        }
        else if (param === 'ADVANCED') {
            submit = false;
        } else {
            $('#chosen-dances').val([param]);
            $('#dances').val(param);
        }
        
        if (submit) {
            $('#chosen-dances').trigger('chosen:updated');
            $('#search').submit();
        } else {
            ShowAdvanced();
        }
    });

    $('#chosen-dances').chosen({ max_selected_options: 5, width: '350px' });

    $('#dance-boolean').find('a').click(function (e) {
        var dances = $('#dances').val();
        var text = ' <span class="caret">';
        var and = dances.indexOf('AND,') === 0;

        if ($(this)[0].id === 'db-any')
        {
            window.danceOr = true;
            text = 'any' + text;
            if (and) {
                dances = dances.substring(4);
            }
        }
        else
        {
            window.danceOr = false;
            text = 'all' + text;
            if (!and) {
                dances = 'AND,' + dances;
            }
        }
        $('#dance-boolean').find('button').html(text);
        $('#dances').val(dances);
    });

    $('#chosen-dances').chosen().change(function () {
        var dances = $('#chosen-dances').val();

        var text = null;
        if (!dances || dances.length === 0) {
            text = 'All Dances';
            $('#dances').val('');
        }
        else if (dances.length === 1) {
            $('#dances').val(dances[0]);

            var danceButton = $('#DID_' + dances[0]);
            text = danceButton.text();
        }
        else if (dances.length > 1) {
            var ret = '';
            if (!window.danceOr) {
                ret = 'AND,';
            }

            ret += dances.join(',');

            $('#dances').val(ret);

            text = 'Advanced';
        }

        var label = $('.search-panel span#dance_selector');
        label.text(text);

        return true;
    });

    //$('#search').submit(function (e) {
    //    var dances = $('#chosen-dances').val();

    //    if (!dances) return true;

    //    if (dances.length === 1) {
    //        $('#dances').val(dances[0]);
    //    }
    //    else if (dances.length > 1) {
    //        var ret = '';
    //        if (!danceOr) {
    //            ret = 'AND,';
    //        }

    //        ret += dances.join(',');

    //        $('#dances').val(ret);
    //    }

    //    return true;
    //});
});

function ShowAdvanced()
{
    var button = '<span id="left-icon" class="glyphicon glyphicon-arrow-up"></span> Less <span id="right-icon" class="glyphicon glyphicon-arrow-up"></span>';

    $('#AdvancedSearch').show();
    var search = $('#search');
    search.attr('action', '/song/advancedsearch');
    $('#ToggleAdvanced').html(button);
}

function ShowBasic()
{
    var button = '<span id="left-icon" class="glyphicon glyphicon-arrow-down"></span> More <span id="right-icon" class="glyphicon glyphicon-arrow-down"></span>';

    $('#AdvancedSearch').hide();
    $('#search').attr('action', '/song/search');
    $('#ToggleAdvanced').html(button);
}

