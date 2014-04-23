// TODONEXT: clean this up both code-wise and UX-wise
// Think about how to add back in some of the stuff
//  that was part of the phone versions

var diag = false;

var counter = 0;
var start = new Date().getTime();

var defWait = 10 * 1000;
var maxCounts = 50;

var intervals = [];

var maxWait = defWait;
var last = new Date().getTime() - defWait;
var average = 0;
var numerator = 4;

var dances = [];

var danceTable = [[],[],[],[],[]];

$(document).ready(function () {
    $("#reset").click(function () { doReset() });
    $("#count").click(function () { doClick() });

    $("#mt1").click(function () { setNumerator(1) });
    $("#mt2").click(function () { setNumerator(2) });
    $("#mt3").click(function () { setNumerator(3) });
    $("#mt4").click(function () { setNumerator(4) });
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

        var old = average;
        average = ms / intervals.length;
        var delta = average - old;
        var dp = delta / average;
        //maxWait = average * 2;

        counter += 1;

        if (Math.abs(delta) >= .1)
        {
            var rate = getRate();
            var dt = danceTable[numerator];
            var d = dt[rate * 10];
            if (d === undefined) {
                var uri = '/api/dance?tempo=' + rate + "&numerator=" + numerator;
                $.getJSON(uri)
                    .done(function (data) {
                        dances = data;
                        dt[rate * 10] = data;
                        display();
                        if (diag)
                            console.log("Fetched: tempo=" + rate + "; numerator=" + numerator);
                    })
                    .fail(function (jqXHR, textStatus, err) {
                        window.alert(err);
                        dances = [];
                        display();
                    });
            }
            else
            {
                if (diag)
                    console.log("PREFETCH: tempo=" + rate + "; numerator=" + numerator);
                dances = d;
                display();
            }
        }
        else
        {
            display();
        }
    }
}

function doReset()
{
    start = new Date().getTime();
    dances = [];
    counter = 0;
    average = 0;
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
    $("#tempo").text(getRate());

    $("#dances").empty();

    for (var i = 0; i < dances.length; i++) {
        var dance = dances[i];
        var text = null;
        if (dance.TempoDelta == 0)
        {
            text = "<div class='list-group-item list-group-item-info'>" + dances[i].Name + "</div>";
        }
        else
        {
            var type = (dance.TempoDelta < 0) ? "list-group-item-danger" : "list-group-item-success";
            text = "<div class='list-group-item " + type + "'><span class='badge'>" + dances[i].TempoDelta + "%</span>" + dances[i].Name + "</div>"
        }

        $("#dances").append(text);
    }
}

function getRate()
{
    if (average === 0)
        return 0.0;
    else
        return (Math.round(60 * 10000 / average) / 10).toFixed(1);
}

function setNumerator(num)
{
    if (numerator != num)
    {
        numerator = num;
        doReset();
    }
}