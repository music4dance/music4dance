function ShowAdvanced() {
    var button = '<span id="left-icon" class="glyphicon glyphicon-arrow-up"></span> Less <span id="right-icon" class="glyphicon glyphicon-arrow-up"></span>';

    $('#AdvancedSearch').show();
    var search = $('#search');
    search.attr('action', '/song/advancedsearch');
    $('#ToggleAdvanced').html(button);
}

function ShowBasic() {
    var button = '<span id="left-icon" class="glyphicon glyphicon-arrow-down"></span> More <span id="right-icon" class="glyphicon glyphicon-arrow-down"></span>';

    $('#AdvancedSearch').hide();
    $('#search').attr('action', '/song/search');
    $('#ToggleAdvanced').html(button);
}

var filter = function() {
    var viewModel = null;

    var tagMapping = {
        'TagSummary': {
            create: function(options) {
                return tagChooser.tagSummary(options.data, options.parent);
            }
        }
    };

    // This is a taggable object, se notes in tagchooser.js for details
    // ReSharper disable once InconsistentNaming
    var TagFilter = function(data, parent, include) {
        var self = this;

        self.page = parent;
        self.isInclude = include;

        self.extraTagTypes = [{ name: 'Music', label: 'Musical Genre' }, { name: 'Style', label: 'Style' }];
        self.changeText = function () { return (include ? 'include' : 'exclude') + 'list.'; }
        self.action = include ? 'include' : 'exclude';

        ko.mapping.fromJS(data, tagMapping, this);

        self.changed = function() {
            // TODO: implement this if we need to track changed.
        }
    };

    var filterMapping = {
        'includeTags': {
            create: function(options) {
                return new TagFilter(options.data, options.parent, true);
            }
        },
        'excludeTags': {
            create: function(options) {
                return new TagFilter(options.data, options.parent, false);
            }
        }
    };

    // ReSharper disable once InconsistentNaming
    var FilterPage = function(data) {
        var self = this;

        ko.mapping.fromJS(data, filterMapping, this);

        self.tagSuggestions = ko.observable(tagChooser.tagSuggestions('song-list', [tagChooser.currentSuggestion('Include'), tagChooser.currentSuggestion('Exclude')]));

        self.canTag = ko.observable(true);
        self.showTag = ko.observable(false);

        self.buildList = function (direction, tags) {
            if (!tags) return null;

            var decorated = [];
            $.each(tags, function(idx, val) { decorated.push(direction + val); });
            return decorated.join('|');
        }

        self.results = function() {
            var inc = self.buildList('+', self.includeTags.CurrentUserTags.Tags());
            var exc = self.buildList('-', self.excludeTags.CurrentUserTags.Tags());
            
            return [inc, exc].join('|');
        };

        self.showTagModal = function (event) {
            var ts = self.tagSuggestions();

            var button = $(event.relatedTarget);
            var direction = button.data('action');
            ts.setCurrent(direction);
            var obj = this[direction + 'Tags'];

            tagChooser.bindModal(ts, obj, button);
        }
    }

    var pageMapping = {
        create: function(options) {
            return new FilterPage(options.data);
        }
    };

    var showTagModal = function(event) {
        viewModel.showTagModal(event);
    };

    var init = function () {
        var filterTags = window.songFilter ? window.songFilter.Tags : null;

        // TOOD: Think about if there is a better factoring here - CurrentUserTags & TagSummary will always be the same in this instance
        //  As a corollary, it appears that even if the will mirror each other, making them the same object fails, so failing the above,
        //    it may be worth checking to see if we can clone rather than rebuilding each object twice.
        var data = {
            includeTags: { CurrentUserTags: tagChooser.buildUserTags(true, filterTags), TagSummary: tagChooser.buildUserTags(true, filterTags) },
            excludeTags: { CurrentUserTags: tagChooser.buildUserTags(false, filterTags), TagSummary: tagChooser.buildUserTags(false, filterTags) },
            changed: false
        };

        viewModel = ko.mapping.fromJS(data, pageMapping);
        tagChooser.setupModal(viewModel.tagSuggestions, showTagModal, { width: '500px' });
        ko.applyBindings(viewModel);

    }

    var update = function () {
        var results = viewModel.results();
        $('#tags').val(results);
    }

    return {
        init: init,
        update: update
    }
}();


$(document).ready(function () {
    // Handling for show/hide advanced search
    $('#ToggleAdvanced').click(function () {
        if (showAdvanced)
        {
            ShowBasic();
        }
        else
        {
            ShowAdvanced();
        }
        window.showAdvanced = !window.showAdvanced;
    });

    if (showAdvanced)
    {
        ShowAdvanced();
    }
    else
    {
        ShowBasic();
    }

    // Handling for Dance selector
    $('.search-panel .dropdown-menu').find('a').click(function (e) {
        e.preventDefault();
        var param = $(this).attr('href').replace('#', '');
        var name = $(this).text();
        $('.search-panel span#dance_selector').text(name);

        var dances = $('.input-group #dances');
        dances.val(param);

        var submit = true;
        if (param === 'ALL') {
            $('#chosen-dances').val([]);
            $('#dances').val('');
        }
        else if (param === 'ADVANCED') {
            submit = false;
        } else {
            $('#chosen-dances').val([param]);
            $('#dances').val(param);
        }
        
        if (submit) {
            $('#chosen-dances').trigger('chosen:updated');
            $('#search').submit();
        } else {
            ShowAdvanced();
        }
    });

    $('#chosen-dances').chosen({ max_selected_options: 5, width: '350px' });

    $('#dance-boolean').find('a').click(function () {
        var dances = $('#dances').val();
        var text = ' <span class="caret">';
        var and = dances.indexOf('AND,') === 0;

        if ($(this)[0].id === 'db-any')
        {
            window.danceOr = true;
            text = 'any' + text;
            if (and) {
                dances = dances.substring(4);
            }
        }
        else
        {
            window.danceOr = false;
            text = 'all' + text;
            if (!and) {
                dances = 'AND,' + dances;
            }
        }
        $('#dance-boolean').find('button').html(text);
        $('#dances').val(dances);
    });

    $('#chosen-dances').chosen().change(function () {
        var dances = $('#chosen-dances').val();

        var text = null;
        if (!dances || dances.length === 0) {
            text = 'All Dances';
            $('#dances').val('');
        }
        else if (dances.length === 1) {
            $('#dances').val(dances[0]);

            var danceButton = $('#DID_' + dances[0]);
            text = danceButton.text();
        }
        else if (dances.length > 1) {
            var ret = '';
            if (!window.danceOr) {
                ret = 'AND,';
            }

            ret += dances.join(',');

            $('#dances').val(ret);

            text = 'Advanced';
        }

        var label = $('.search-panel span#dance_selector');
        label.text(text);

        return true;
    });

    $('#search').submit(function () { filter.update(); });

    filter.init();
});

