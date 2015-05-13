var diag = false;

// These variables all have optional input variables named "param*" to set up
//  different variations
var showBPM = false;
var showMPM = true;
var numerator = 4;
var defVisible = .05;
var epsVisible = defVisible;

var counter = 0;
var start = new Date().getTime();

var defWait = 5 * 1000;
var maxCounts = 50;

var intervals = [];

var maxWait = defWait;
var last = 0;
var average = 0;
var rate = 0.0;
var epsExact = .01;

var dances = [];
var danceIndex = null;

var timeout = null;

var labels = ["BPM ", "2/4 MPM ", "3/4 MPM ", "4/4 MPM "];

var tempoId = "#Tempo";
var mpmId = "#MPM";

var danceAction = function (id) {
    for (var i = 0; i < dances.length; i++)
    {
        if (dances[i].Id === id)
        {
            name = dances[i].SeoName;
        }
    }

    if (name)
        window.location.href = '/dances/' + name;
}

$(document).ready(function () {
    if (typeof paramShowBPM === 'boolean')
    {
        showBPM = paramShowBPM;
    }

    if (typeof paramShowMPM === 'boolean')
    {
        showMPM = paramShowMPM;
    }

    if (typeof paramNumerator === 'number') {
        setNumeratorControl(paramNumerator);
    }

    if (typeof paramTempo == 'number') {
        $(tempoId).val(paramTempo);
    }

    if (typeof paramEpsVisible === 'number') {
        defvisible = epsVisible = paramEpsVisible;
        $("#epsilon").val(epsVisible * 100);
    }

    $("#reset").click(function () { doReset() });
    $("#count").click(function () { doClick() });

    $("#mt1").click(function () { setNumerator(1) });
    $("#mt2").click(function () { setNumerator(2) });
    $("#mt3").click(function () { setNumerator(3) });
    $("#mt4").click(function () { setNumerator(4) });

    $("#epsilon").change(function() {
        setEpsilon($(this).val());
    });

    var uri = '/api/dance';
    $.getJSON(uri)
        .done(function (data) {
            setupDances(data);
        })
        .fail(function (jqXHR, textStatus, err) {
            window.alert(err);
        });

    $(mpmId).each(function() {
        // Save current value of element
        $(this).data('oldVal', $(this));

        // Look for changes in the value
        $(this).bind("propertychange keyup input paste", function(event){
            // If value has changed...
            if ($(this).data('oldVal') !== $(this).val()) {
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

    $(tempoId).each(function() {
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
                    updateRate(roundTempo(newRate/numerator));
                }
                // Else user error here
            }
        });
    });

    var epsilon = $('#epsilon');
    if (epsilon.length > 0) {
        epsilon.noUiSlider({
            start: [5],
            step: 1,
            range: { 'min': [1], 'max': [50] }
        });
    }
    //$('#tempo').focus(function () { setFocus(true);});
});

function doClick()
{
    if (timeout)
    {
        window.clearTimeout(timeout);
    }
    var current = new Date().getTime();
    if (last === 0) {
        last = current;
        display();
    }
    else
    {
        var delta = current - last;
        last = current;

        if (delta >= maxWait) {
            doReset();
        }
        else {
            intervals.push(delta);
            if (intervals.length > maxCounts)
                intervals.shift();

            var ms = 0;
            for (var i = 0; i < intervals.length; i++) {
                ms += intervals[i];
            }

            var old = average;
            average = ms / intervals.length;
            var delta = average - old;
            var dp = delta / average;
            //maxWait = average * 2;

            counter += 1;

            if (Math.abs(delta) >= .1) {
                updateRate(getRate())
            }
            else {
                display();
            }
        }
    }

    timeout = window.setTimeout(function () { timerReset();},maxWait)
}

function timerReset(noRefresh) {
    start = new Date().getTime();
    counter = 0;
    average = 0;
    intervals = [];
    //rate = 0;
    last = 0;
    if (!noRefresh)
    {
        display();
    }
}

function doReset()
{
    dances = [];
    rate = average.toFixed(1);
    maxWait = defWait;
    timerReset();
    epsVisible = defVisible;

    $("#epsilon").val(epsVisible * 100);
}

function formatTempo(range,meter)
{
    if (!showMPM && !showBPM)
        return "";

    var ret = "<small>(";

    if (showMPM)
    {
        ret += range.Min;
        if (range.Min != range.Max) {
            ret += "-" + range.Max;
        }
        ret += " MPM " + meter.Numerator + "/4";

        if (showBPM)
        {
            ret += " & ";
        }
    }

    if (showBPM)
    {
        ret += range.Min * meter.Numerator;
        if (range.Min != range.Max) {
            ret += "-" + range.Max * meter.Numerator;
        }
        ret += "BPM ";
    }

    ret += ")<small>";
    return ret;
}

function display() {
    $("#total").text(counter);
    var t = new Date().getTime();
    var dt = t - start;
    $("#time").text(dt);
    $("#avg").text(Math.round(average));
    $("#rate").text(rate);

    if (!$(mpmId).is(":focus") || rate === 0)
    {
        $(mpmId).val(rate);
    }
    if (!$(tempoId).is(":focus") || rate === 0) {
        $(tempoId).val(roundTempo(rate * numerator));
    }

    var bt = last == 0 ? 'Count' : 'Again';
    $("#count").html(bt);
    $("#dances").empty();

    var idpfx = "add-dance-"
    for (var i = 0; i < dances.length; i++) {
        var dance = dances[i];
        var text = "<a id='" + idpfx + dance.Id + "' href='#' class='list-group-item ";

        if (dance.TempoDelta == 0)
        {
            text += " list-group-item-info'>";
        }
        else
        {
            var type = (dance.TempoDelta < 0) ? "list-group-item-danger" : "list-group-item-success";
            text += type + "'>" +
                "<span class='badge'>" + dances[i].TempoDelta + "MPM</span>";
                
        }
        var strong = numerator === 1 || numerator === dance.Meter.Numerator;
        if (strong) text += "<strong>";
        text += dances[i].Name;
        if (strong) text += "</strong>";
        text += " " + formatTempo(dance.TempoRange, dance.Meter) + "</div>";

        $("#dances").append(text);

        if (typeof danceAction == 'function')
        {
            $("#add-dance-" + dance.Id).click(function () {
                var id = $(this).context.id;
                id = id.substring(idpfx.length);
                danceAction(id);
            });
        }
    }

    $('#help').toggle(dances.length === 0);
}

function updateDances()
{
    dances = [];

    var bpm = rate * numerator;

    for (var i = 0; i < danceIndex.length; i++) {
        var dance = danceIndex[i];

        if (numerator === 1 || dance.Meter.Numerator === numerator ||
            (numerator == 2 && dance.Meter.Numerator == 4) || (numerator == 4 && dance.Meter.Numerator == 2)) {
            var delta = NaN;

            var tempRate = bpm / dance.Meter.Numerator;

            if (tempRate < dance.TempoRange.Min) {
                delta = tempRate - dance.TempoRange.Min;
            }
            else if (tempRate > dance.TempoRange.Max) {
                delta = tempRate - dance.TempoRange.Max;
            }
            else {
                delta = 0;
            }

            avg = (dance.TempoRange.Min + dance.TempoRange.Max) / 2;
            eps = delta / avg;

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

function updateRate(newRate)
{
    console.log("Rate=" + newRate);
    if (rate === newRate)
    {
        return;
    }

    rate = newRate;

    updateDances();
}

function roundTempo(t)
{
    if ($.isNumeric(t))
    {
        var r = Math.round(t * 10) / 10;
        return Number(r.toFixed(1));
    }
    else
    {
        return 0.0;
    }
}

function getRate()
{
    var ret = 0;
    if (average !== 0)
        ret = 60 * 1000 / average;

    return roundTempo(ret);
}

function rateFromText(text)
{
    var r = Number(text);
    return roundTempo(r);
}

function setupDances(data)
{
    danceIndex = data;

    var tempoT = rateFromText($(tempoId).val());
    var mpmT = rateFromText($(mpmId).val());

    if (mpmT !== 0) {
        updateRate(mpmT);
    }
    else if (tempoT !== 0) {
        updateRate(roundTempo(tempoT / numerator));
    }
}

function setNumeratorControl(num)
{
    if (numerator !== num) {
        $("#mt").empty();
        $("#mt").append(labels[num - 1]);
        $("#mt").append("<span class='caret'></span>");
        numerator = num;
    }
}

function setNumerator(num)
{
    if (numerator !== num)
    {
        var old = numerator;
        setNumeratorControl(num);

        var r = (old * rate) / num;
        r = roundTempo(r);

        timerReset(rate !== 0);
        if (rate !== 0)
        {
            updateRate(r);
        }
    }
}

function setEpsilon(newEpsI)
{
    var newEps = newEpsI / 100;
    if (newEps !== epsVisible)
    {
        epsVisible = newEps;
        updateDances();
    }
}
