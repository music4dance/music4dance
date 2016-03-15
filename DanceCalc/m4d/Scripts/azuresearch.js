$(document).ready(function() {
    //var test = {
    //    suggestions :[
    //    { value: 'swing', data: 'SWG'},
    //    { value: 'jump swing', data: 'JSW'},
    //    { value: 'east coast swing', data: 'ECS' },
    //    { value: 'west coast swing', data: 'WCS' }
    //]};

    $('#search-text').autocomplete({
        //lookup: function (query, done) {
        //    done(test);
        //},
        //lookup: test,
        //lookup: function (query, done) {
        //    var s = encodeURIComponent(query);
        //    $.ajax({
        //        url: 'https://m4d.search.windows.net/indexes/songs/docs/suggest',
        //        method: 'post',
        //        dataType: 'jsonp',
        //        data: {
        //            suggesterName:'songs',
        //            top :15,
        //            search : s
        //        },
        //        xhrFields: {
        //            withCredentials: true
        //        },
        //        headers: {
        //            'api-version': '2015-02-28',
        //            'api-key': '5B2BAFC30F0CD25405A10B08582B5451'
        //        },
        //        success: function(data) {
        //            done(data);
        //        },
        //        error: function(xhr, status, thrown) {
        //            alert(status);
        //        }
        //    });
        //},
        lookup: function(query, done) {
            var s = encodeURIComponent(query);
            $.getJSON('/api/suggestion/?id=' + s)
                .done(function (data) {
                    if (!data || !data.suggestions) {
                        console.log('Bad Query: ' + query);
                    } else {
                        done(data);
                    }
                })
                .fail(function (jqXhr, textStatus) {
                    console.log(textStatus);
                });
        }
        //,onSelect: function(suggestion) {}
    });
});