$(document).ready(function() {
    //var test = {
    //    suggestions :[
    //    { value: 'swing', data: 'SWG'},
    //    { value: 'jump swing', data: 'JSW'},
    //    { value: 'east coast swing', data: 'ECS' },
    //    { value: 'west coast swing', data: 'WCS' }
    //]};

    $('#search-text').autocomplete({
        lookup: function(query, done) {
            var s = encodeURIComponent(query);
            $.getJSON('/api/suggestion/' + s)
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
    });
});