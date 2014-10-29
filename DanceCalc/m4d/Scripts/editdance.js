/// Helper functions

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

//var linksMapping = {
//    'links': {
//        create: function (options) {
//            return new Link(options.data);
//        }
//    }
//}

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
});