var filter = function () {
    var viewModel = null;

    var tagMapping = {
        'TagSummary': {
            create: function (options) {
                return tagChooser.tagSummary(options.data, options.parent);
            }
        }
    };

    // This is a taggable object, se notes in tagchooser.js for details
    // ReSharper disable once InconsistentNaming
    var TagFilter = function (data, parent, include) {
        var self = this;

        self.page = parent;
        self.isInclude = include;

        self.extraTagTypes = [{ name: 'Music', label: 'Musical Genre' }, { name: 'Style', label: 'Style' }];
        self.changeText = function () { return (include ? 'include' : 'exclude') + 'list.'; }
        self.action = include ? 'include' : 'exclude';

        ko.mapping.fromJS(data, tagMapping, this);

        self.changed = function () {
            // TODO: implement this if we need to track changed.
        }

        self.clear = function () {
            self.CurrentUserTags.Tags.removeAll();
            self.TagSummary.Tags.removeAll();
            console.log(self.CurrentUserTags.Summary().Summary);
        }
    };

    var filterMapping = {
        'includeTags': {
            create: function (options) {
                return new TagFilter(options.data, options.parent, true);
            }
        },
        'excludeTags': {
            create: function (options) {
                return new TagFilter(options.data, options.parent, false);
            }
        }
    };

    // ReSharper disable once InconsistentNaming
    var FilterPage = function (data) {
        var self = this;

        ko.mapping.fromJS(data, filterMapping, this);

        self.tagSuggestions = ko.observable(tagChooser.tagSuggestions('song-list', [tagChooser.currentSuggestion('Include'), tagChooser.currentSuggestion('Exclude')]));

        self.canTag = ko.observable(true);
        self.showTag = ko.observable(false);

        self.buildList = function (direction, tags) {
            if (!tags) return null;

            var decorated = [];
            $.each(tags, function (idx, val) { decorated.push(direction + val); });
            return decorated.join('|');
        }

        self.results = function () {
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

            tagChooser.bindModal(ts, obj, button, $(event.currentTarget));
        }

        self.clear = function () {
            self.includeTags.clear();
            self.excludeTags.clear();
        }
    }

    var pageMapping = {
        create: function (options) {
            return new FilterPage(options.data);
        }
    };

    var showTagModal = function (event) {
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
        if (event.target.id === 'search') {
            var results = viewModel.results();
            $('#tags').val(results);
        } else {
            console.log('reset');
        }
    }

    var reset = function (event) {
        event.preventDefault();

        // Uncheck all of the checkboxes and sets all of the hidden and text fields to empty strings
        $('#search').find('input[type=checkbox]').attr('checked', false);
        $('#search').find('input[type=hidden]').val('');
        $('#search').find('input[type=text]').val('');

        // Clear out all the dances and update the control
        $('#chosen-dances').val(null);
        $('#chosen-dances').trigger('chosen:updated');

        // Set the Any/All dance button back to any
        $('#dance-boolean').find('button').html('any <span class="caret"></span>');

        // Clear all of the tags
        viewModel.clear();
    }

    return {
        init: init,
        update: update,
        reset: reset
    }
}();


$(document).ready(function () {
    // Handling for Dance selector
    $('.search-panel .dropdown-menu').find('a').click(function (e) {
        e.preventDefault();
        var param = $(this).attr('href').replace('#', '');
        var name = $(this).text();
        $('.search-panel span#dance_selector').text(name);

        $('.input-group #dances').val((param === 'ALL') ? '' : param);

        $('#search').submit();
    });

    // Handle advanced search link
    $('#advanced-search').click(function () {
        var txt = $('#search-text').val();
        var href = $(this).attr('href');

        if (!txt) txt = '.';

        var filterName = 'filter=';
        var ich = href.indexOf(filterName);
        if (ich === -1) {
            href += "?filter=advanced-.-.-" + txt;
        } else {
            var filter = href.substring(ich + filterName.length);
            var params = filter.split('-');
            for (var i = 0; i < 3; i++) {
                if (!params[i]) params[i] = '.';
            }
            params[3] = txt;
            filter = params.join('-');
            href = href.substring(0, ich + filterName.length) + filter;
        }
        
        $(this).attr('href', href);
    });

    // Handle basic search link
    $('#basic-search').click(function () {
        var txt = $('#keyword').val();
        if (!txt) txt = '.';

        var dances = $('#dances').val();
        if (dances.indexOf(',') !== -1) {
            dances = '.';
        }
        var sortColumn = $('input[name=sortOrder]:checked').val();
        var sortDirection = $('input[name=sortDirection]:checked').val();
        var sort = sortColumn + (sortDirection === 'Descending' ? '_desc' : '');
            
        var href = $(this).attr('href');
        href += '?filter=Index-' + dances + '-' + sort + '-' + txt;

        var filterName = 'filter=';
        var ich = href.indexOf(filterName);
        if (ich === -1) {
            href += "?filter=advancedsearch-.-.-" + txt;
        } else {
            var filter = href.substring(ich + filterName.length);
            var params = filter.split('-');
            for (var i = 0; i < 3; i++) {
                if (!params[i]) params[i] = '.';
            }
            params[3] = txt;
            filter = params.join('-');
            href = href.substring(0, ich + filterName.length) + filter;
        }

        $(this).attr('href', href);
    });

    $('#chosen-dances').chosen({ max_selected_options: 5, width: '100%' });

    $("input[name='db']", $('#dance-boolean')).change(function () {
        var dances = $('#dances').val();
        var and = dances.indexOf('AND,') === 0;

        if ($(this).val() === 'any') {
            window.danceOr = true;
            if (and) {
                dances = dances.substring(4);
            }
        } else {
            window.danceOr = false;
            if (!and) {
                dances = 'AND,' + dances;
            }
        }
        $('#dances').val(dances);
    });

    $('#chosen-dances').chosen().change(function () {
        var dances = $('#chosen-dances').val();

        if (!dances || dances.length === 0) {
            $('#dances').val('');
        }
        else if (dances.length === 1) {
            $('#dances').val(dances[0]);
        }
        else if (dances.length > 1) {
            var ret = '';
            if (!window.danceOr) {
                ret = 'AND,';
            }

            ret += dances.join(',');

            $('#dances').val(ret);
        }

        return true;
    });

    $('#search').submit(function () { filter.update(); });
    $('#reset-search').click(function () { filter.reset(event); });

    // Setup tool-tips
    $('[data-toggle="tooltip"]').tooltip();

    filter.init();
});

