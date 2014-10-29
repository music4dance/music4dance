var OnDance = function(evt)
{
    evt.preventDefault();
    window.location.href = '/Dances/' + evt.target.id;
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
