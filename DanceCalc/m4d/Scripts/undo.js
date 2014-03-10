$(document).ready(function () {
    for (var i = 0; i < lineNumbers.length; i++) {
        var name = "#Arrow-" + lineNumbers[i].toString();
        $(name).click(i, FillDown)
    }
});

function FillDown(event) {
    var started = false;
    var fill = false;
    for (var i = 0; i < lineNumbers.length; i++) {
        var name = "#Undo-" + lineNumbers[i].toString();
        if (i == event.data)
        {
            started = true;
            fill = $(name).is(':checked');
        }
        else if (started)
        {
            $(name).prop('checked',fill);
        }
    }
}