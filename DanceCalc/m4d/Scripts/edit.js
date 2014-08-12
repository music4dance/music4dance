$(document).ready(function () {
    for (var i = 0; i < albumCount; i++)
    {
        var name = "#Delete_" + i.toString();
        $(name).click(i,deleteAlbum)
    }

    $('#AddAlbum').click(addAlbum);

    $('#counter-control').hide();
    $('#toggle-count').click(function () {
        var visible = $('#counter-control').is(':visible');
        if (visible)
        {
            $('#counter-control').hide();
            $('#toggle-count-display').removeClass('glyphicon-arrow-up');
            $('#toggle-count-display').addClass('glyphicon-arrow-down');
        }
        else
        {
            $('#counter-control').show();
            $('#toggle-count-display').removeClass('glyphicon-arrow-down');
            $('#toggle-count-display').addClass('glyphicon-arrow-up');
        }
    });
});

function deleteAlbum(event)
{
    var id = "#Album_" + event.data.toString();
    var base = "#Albums_" + event.data.toString();
    var name = base + "__Name";
    var track = base + "__Track";
    var publisher = base + "__Publisher";

    $(id).hide();

    $(name).val(null);
    $(track).val(null);
    $(publisher).val(null);
}

function addAlbum()
{
    var name = "#Album_" + (albumCount + albumsAdded).toString();

    albumsAdded += 1;
    $(name).show();

    if (albumsAdded == 4)
    {
        $('#AddAlbum').hide();
    }
}

function danceAction(id)
{
    Debug.write("Dance Id=" + id + "\r\n");
    $('#addDances option[value="' + id + '"]').attr('selected', 'selected');
    $('#addDances').trigger('chosen:updated');

}