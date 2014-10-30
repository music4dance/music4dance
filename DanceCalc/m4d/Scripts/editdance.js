/// Helper functions

//function initToolbarBootstrapBindings() {
//    var fonts = ['Serif', 'Sans', 'Arial', 'Arial Black', 'Courier',
//            'Courier New', 'Comic Sans MS', 'Helvetica', 'Impact', 'Lucida Grande', 'Lucida Sans', 'Tahoma', 'Times',
//            'Times New Roman', 'Verdana'],
//            fontTarget = $('[title=Font]').siblings('.dropdown-menu');
//    $.each(fonts, function (idx, fontName) {
//        fontTarget.append($('<li><a data-edit="fontName ' + fontName + '" style="font-family:\'' + fontName + '\'">' + fontName + '</a></li>'));
//    });
//    $('a[title]').tooltip({ container: 'body' });
//    $('.dropdown-menu input').click(function () { return false; })
//        .change(function () { $(this).parent('.dropdown-menu').siblings('.dropdown-toggle').dropdown('toggle'); })
//    .keydown('esc', function () { this.value = ''; $(this).change(); });

//    $('[data-role=magic-overlay]').each(function () {
//        var overlay = $(this), target = $(overlay.data('target'));
//        overlay.css('opacity', 0).css('position', 'absolute').offset(target.offset()).width(target.outerWidth()).height(target.outerHeight());
//    });
//    if ("onwebkitspeechchange" in document.createElement("input")) {
//        var editorOffset = $('#editor').offset();
//        $('#voiceBtn').css('position', 'absolute').offset({ top: editorOffset.top, left: editorOffset.left + $('#editor').innerWidth() - 35 });
//    } else {
//        $('#voiceBtn').hide();
//    }
//};

//function showErrorAlert(reason, detail) {
//    var msg = '';
//    if (reason === 'unsupported-file-type') { msg = "Unsupported format " + detail; }
//    else {
//        console.log("error uploading file", reason, detail);
//    }
//    $('<div class="alert"> <button type="button" class="close" data-dismiss="alert">&times;</button>' +
//        '<strong>File upload error</strong> ' + msg + ' </div>').prependTo('#alerts');
//};

function copyToEdit() {
    var t = $('#editor').cleanHtml();

    var rg = /http:\/\/([^\/\.]*)\//gi;
    t = t.replace(rg, "/$1/");

    rg = /<p[^>]*>/gi;
    t = t.replace(rg, "<p>");

    rg = /<\/?font[^>]*>/gi;
    t = t.replace(rg, "");

    rg = /&nbsp;/gi;
    t = t.replace(rg, " ");

    $('#description').val(t);
}

function pasteToRichEdit() {
    var s = $('#description').val();
    $('#editor').html(s);
}

var computeLinkName = function (idx, name) {
    var ret = "DanceLinks[" + idx + "]." + name;
    return ret;
}

var computeLinkId = function (idx, name) {
    var ret = "DanceLinks_" + idx + "__" + name;
    return ret;
}

var EditPage = function (data)
{
    var self = this;

    ko.mapping.fromJS(data, null, this);

    self.newLink = function () {
        var temp = ko.mapping.fromJS({ Id: '{00000000-0000-0000-0000-000000000000}', Description: null, Link: null });
        self.links.push(temp);
    };

    self.removeLink = function (data, event) {
        event.preventDefault();
        var id = event.target.id;
        var arr = id.split("_");
        var idx = arr[1];
        self.links.splice(idx,1);
    };
}

var pageMapping = {
    create: function(options)
    {
        return new EditPage(options.data);
    }
};

var viewModel;

$(document).ready(function () {
    var data = { links: links };
    viewModel = ko.mapping.fromJS(data, pageMapping);

    ko.applyBindings(viewModel);
    //initToolbarBootstrapBindings({ fileUploadError: showErrorAlert });

    pasteToRichEdit();
    $('#editor').wysiwyg();
    $('#copy').click(function () { copyToEdit(); });
    $('#paste').click(function () { pasteToRichEdit(); });
    $('#submit').click(function () { copyToEdit(); });
});