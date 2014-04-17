var counter = 0;
var start = new Date().getTime();

var defWait = 10 * 1000;
var maxCounts = 50;

var intervals = [];

var maxWait = defWait;
var last = new Date().getTime() - defWait;
var average = 0;


$(document).ready(function () {
    $("#reset").click(function () { doReset() });
    $("#count").click(function () { doClick() });
});

function doClick()
{
    var current = new Date().getTime();
    var delta = current - last;
    last = current;

    if (delta >= maxWait)
    {
        doReset();
    }
    else
    {
        intervals.push(delta);
        if (intervals.length > maxCounts)
            intervals.shift();

        var ms = 0;
        for (var i = 0; i < intervals.length; i++)
        {
            ms += intervals[i];
        }

        average = ms / intervals.length;
        //maxWait = average * 2;

        counter += 1;
        display();
    }
}

function doReset()
{
    start = new Date().getTime();
    counter = 0;
    intervals = [];
    last = new Date().getTime();
    maxWait = defWait;
    display();
}

function display() {
    $("#total").text(counter);
    var t = new Date().getTime();
    var dt = t - start;
    $("#time").text(dt);
    $("#avg").text(Math.round(average));
    $("#rate").text(getRate());
}

function getRate()
{
    if (average === 0)
        return 0;
    else
        return Math.round(60*1000/average);
}