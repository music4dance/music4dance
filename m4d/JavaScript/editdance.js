/// Helper functions

//function copyToEdit() {
//    var t = $('#editor').cleanHtml();

//    var rg = /http:\/\/([^\/\.]*)\//gi;
//    t = t.replace(rg, "/$1/");

//    rg = /<p[^>]*>/gi;
//    t = t.replace(rg, "<p>");

//    rg = /<\/?font[^>]*>/gi;
//    t = t.replace(rg, "");

//    rg = /&nbsp;/gi;
//    t = t.replace(rg, " ");

//    rg  = /<span style="mso-spacerun: yes;"> *<\/span>/gi;
//    t = t.replace(rg, " ");

//    $('#description').val(t);
//}

//function pasteToRichEdit() {
//    var s = $('#description').val();
//    $('#editor').html(s);
//}

var computeLinkName = function (idx, name) {
    var ret = 'DanceLinks[' + idx + '].' + name;
    return ret;
};

var computeLinkId = function (idx, name) {
    var ret = 'DanceLinks_' + idx + '__' + name;
    return ret;
};

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
        var arr = id.split('_');
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

    $('textarea.mdd_editor').MarkdownDeep({
        help_location: './mdd_help.htm',
        disableTabHandling: true,
        resizebar: true
    });
    //initToolbarBootstrapBindings(); //fileUploadError: showErrorAlert 

    //pasteToRichEdit();
    //$('#editor').wysiwyg();
    //$('#copy').click(function () { copyToEdit(); });
    //$('#paste').click(function () { pasteToRichEdit(); });
    //$('#submit').click(function () { copyToEdit(); });
});