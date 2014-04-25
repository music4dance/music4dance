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
var last = 0;
var average = 0;
var numerator = 4;
var rate = 0.0;
var epsExact = .01;
var epsVisible = .05;

var dances = [];
var danceIndex = null;

var labels = ["BPM ", "2/4 MPM ", "3/4 MPM ", "4/4 MPM "];


$(document).ready(function () {
    $("#reset").click(function () { doReset() });
    $("#count").click(function () { doClick() });

    $("#mt1").click(function () { setNumerator(1) });
    $("#mt2").click(function () { setNumerator(2) });
    $("#mt3").click(function () { setNumerator(3) });
    $("#mt4").click(function () { setNumerator(4) });

    var uri = '/api/dance';
    $.getJSON(uri)
        .done(function (data) {
            setupDances(data);
        })
        .fail(function (jqXHR, textStatus, err) {
            window.alert(err);
        });


    $('#tempo').each(function() {
        // Save current value of element
        $(this).data('oldVal', $(this));

        // Look for changes in the value
        $(this).bind("propertychange keyup input paste", function(event){
            // If value has changed...
            if ($(this).data('oldVal') != $(this).val()) {
                // Updated stored value
                $(this).data('oldVal', $(this).val());
                
                var newRate = rateFromText($(this).val());
                if ($.isNumeric(newRate))
                {
                    updateRate(newRate);
                }
                // Else user error here
            }
        });

    });

    //$('#tempo').focus(function () { setFocus(true);});
});

function doClick()
{
    var current = new Date().getTime();
    if (last === 0) {
        last = current;
        return;
    }

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
            updateRate(getRate())
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
    rate = average.toFixed(1);
    intervals = [];
    last = 0;
    maxWait = defWait;
    display();
}

function display() {
    $("#total").text(counter);
    var t = new Date().getTime();
    var dt = t - start;
    $("#time").text(dt);
    $("#avg").text(Math.round(average));
    $("#rate").text(rate);

    if (!$("#tempo").is(":focus") || rate === 0)
    {
        $("#tempo").val(rate);
    }

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
            text = "<div class='list-group-item " + type + "'><span class='badge'>" + dances[i].TempoDelta + "MPM</span>" + dances[i].Name + "</div>"
        }

        $("#dances").append(text);
    }
}

function updateRate(newRate)
{
    console.log("Rate=" + newRate);
    if (rate == newRate)
    {
        return;
    }

    rate = newRate;
    dances = [];

    for (var i = 0; i < danceIndex.length; i++)
    {
        var dance = danceIndex[i];

        if (numerator === 1 || dance.Meter.Numerator === numerator)
        {
            var delta = NaN;
            
            var tempRate = newRate;

            if (numerator === 1)
            {
                tempRate = newRate / dance.Meter.Numerator;
            }

            if (tempRate < dance.TempoRange.Min) {
                delta = tempRate - dance.TempoRange.Min;
            }
            else if (tempRate > dance.TempoRange.Max) {
                delta = tempRate - dance.TempoRange.Max;
            }
            else {
                delta = 0;
            }

            avg = (dance.TempoRange.Min + dance.TempoRange.Max)/2;
            eps = delta / avg;

            //if (eps < epsExact) {
            //    eps = 0;
            //    delta = 0;
            //}

            if (Math.abs(eps) < epsVisible) {
                dance.TempoDelta = delta.toFixed(1);
                dance.TempoEps = eps;

                dances.push(dance);
            }
        }
    }

    dances.sort(function (a, b) {
        return Math.abs(a.TempoEps) - Math.abs(b.TempoEps);
    });

    display();
}

function getRate()
{
    var ret = 0;
    if (average !== 0)
        ret = Math.round(60 * 10000 / average) / 10;

    return ret.toFixed(1);
}

function rateFromText(text)
{
    var r = Number(text);
    if ($.isNumeric(r))
    {
        r = r.toFixed(1);
    }

    return r;
}

function setupDances(data)
{
    danceIndex = data;
}

function setNumerator(num)
{
    if (numerator != num)
    {
        numerator = num;
        $("#mt").empty();
        $("#mt").append(labels[numerator - 1]);
        $("#mt").append("<span class='caret'></span>");
        doReset();
    }
}