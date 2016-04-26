$(document).ready(function () {
    // Handling for Dance selector
    $('.search-panel .dropdown-menu').find('a').click(function (e) {
        // Testing to see if somehow we screwed a hash algorithm...
        e.preventDefault();
        var param = $(this).attr('href').replace('#', '');
        var name = $(this).text();
        $('.search-panel span#dance_selector').text(name);

        $('.input-group #dances').val(param);

        $('#search').submit();
    });

    // Handle advanced search link
    $('#advanced-search').click(function () {
        var txt = $('#search-text').val();
        var href = $(this).attr('href');

        if (!txt) txt = '.';

        var filterName = 'filter=';
        var ich = href.indexOf(filterName);
        if (ich === -1) {
            href += '?filter=advanced-.-.-' + txt;
        } else {
            var filter = href.substring(ich + filterName.length);
            var params = filter.split('-');
            for (var i = 0; i < 3; i++) {
                if (!params[i]) params[i] = '.';
            }
            params[3] = txt;
            filter = params.join('-');
            href = href.substring(0, ich + filterName.length) + filter;
        }
        
        $(this).attr('href', href);
    });

    // Handle basic search link
    $('#basic-search').click(function () {
        var txt = $('#keyword').val();
        if (!txt) txt = '.';

        var dances = $('#dances').val();
        if (dances.indexOf(',') !== -1) {
            dances = '.';
        }
        var sortColumn = $('input[name=sortOrder]:checked').val();
        var sortDirection = $('input[name=sortDirection]:checked').val();
        var sort = sortColumn + (sortDirection === 'Descending' ? '_desc' : '');
            
        var href = $(this).attr('href');
        href += '?filter=Index-' + dances + '-' + sort + '-' + txt;

        var filterName = 'filter=';
        var ich = href.indexOf(filterName);
        if (ich === -1) {
            href += '?filter=advancedsearch-.-.-' + txt;
        } else {
            var filter = href.substring(ich + filterName.length);
            var params = filter.split('-');
            for (var i = 0; i < 3; i++) {
                if (!params[i]) params[i] = '.';
            }
            params[3] = txt;
            filter = params.join('-');
            href = href.substring(0, ich + filterName.length) + filter;
        }

        $(this).attr('href', href);
    });
});

