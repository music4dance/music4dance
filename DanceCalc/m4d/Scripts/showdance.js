var OnDance = function(evt)
{
    var target = $(evt.target);
    var id = null;
    do {
        id = target.attr('id');
        target = target.parent();
    }
    while (!id)

    evt.preventDefault();
    window.location.href = '/Dances/' + id;

}

var OnSong = function(evt)
{
    evt.stopPropagation();
    window.location.href = '/Song/Search?dances=' + evt.target.id;
}

$(document).ready(function () {
    $('.dance').click(function (evt) { OnDance(evt); });
    $('.badge').click(function (evt) { OnSong(evt); });
});
