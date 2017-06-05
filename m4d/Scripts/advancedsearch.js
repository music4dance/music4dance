var filter = function () {
    var viewModel = null;

    var tagMapping = {
        'TagSummary': {
            create: function (options) {
                return tagChooser.tagSummary(options.data, options.parent);
            }
        }
    };

    // This is a taggable object, see notes in tagchooser.js for details
    // ReSharper disable once InconsistentNaming
    var TagFilter = function (data, parent, include) {
        var self = this;

        self.page = parent;
        self.isInclude = include;

        self.extraTagTypes = [{ name: 'Music', label: 'Musical Genre' }, { name: 'Style', label: 'Style' }];
        self.changeText = function () { return (include ? 'include' : 'exclude') + ' tags.'; }
        self.action = include ? 'include' : 'exclude';

        ko.mapping.fromJS(data, tagMapping, this);

        self.changed = function () {
            // TODO: implement this if we need to track changed.
        }

        self.clear = function () {
            self.CurrentUserTags.Tags.removeAll();
            self.TagSummary.Tags.removeAll();
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
        // TODO: This is a kludge to work around the fact that somewhere upstream there are spaces
        //  being inserted into the taglist - figure out where and then we can get rid of this...
        if (filterTags) {
            var ftList = filterTags.split('|');
            for (var i = 0; i < ftList.length; i++) {
                ftList[i] = ftList[i].trim();
            }
            filterTags = ftList.join('|');
        }

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

    var update = function (event) {
        if (event.target.id === 'search') {
            var results = viewModel.results();
            $('#tags').val(results);
        } else {
            console.log('reset');
        }
    }

    var updateDances = function () {
        var dances = $('#dances').val();

        if (/(AND|ADX|OOX),/gi.test(dances)) {
            dances = dances.substring(4);
        }

        var or = $('#db-any').prop('checked');
        var inf = $('#inferred').prop('checked');

        if (inf) {
            dances = (or ? 'OOX' : 'ADX') + ',' + dances;
        }
        else if (!or) {
            dances = 'AND,' + dances;
        }
        $('#dances').val(dances);

        window.danceOr = or;
        window.danceInferred = inf;
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
        $('#db-any').prop('checked', true);

        // Handle the My Activity Button
        if (window.userName === '') {
            $('#user').val('null');
        } else {
            $('#user').val('-' + window.userName + '|h');
        }

        // Set the SortBy radio buttons to Default/Ascending
        $('input:radio[name=sortOrder]').val(['Closest Match']);
        $('input:radio[name=sortDirection]').val(['Ascending']);

        // Clear all of the tags 
        viewModel.clear();
    }

    return {
        init: init,
        update: update,
        updateDances: updateDances,
        reset: reset
    }
}();


$(document).ready(function () {
    $('#chosen-dances').chosen({ max_selected_options: 5, width: '100%' });

    $('input[name=\'db\']', $('#dance-boolean')).change(function () {
        filter.updateDances();
    });

    $('#inferred').change(function () {
        filter.updateDances();
    });

    $('#chosen-dances').chosen().change(function () {
        var dances = $('#chosen-dances').val();

        var ret = '';
        //if (dances.length === 1) {
        //    ret = dances[0];
        //}
        //else 
        if (dances && dances.length > 0) {
            ret = dances.join(',');
        }
        $('#dances').val(ret);
        filter.updateDances();

        return true;
    });

    // Handle basic search link
    $('#basic-search').click(function () {
        var txt = $('#search-text').val();
        if (!txt) txt = '.';

        var dances = $('#dances').val();
        if (dances.indexOf(',') !== -1) {
            dances = '.';
        }
        var sortColumn = $('input[name=sortOrder]:checked').val();
        var sortDirection = $('input[name=sortDirection]:checked').val();
        var sort = sortColumn + (sortDirection === 'Descending' ? '_desc' : '');

        var href = $(this).attr('href');
        var target = $(this).data('type');
        href += '?filter=' + target + '-' + dances + '-' + sort + '-' + txt;

        //// TODO: Not sure that this ever gets called with an existing filter???
        //var filterName = 'filter=';
        //var ich = href.indexOf(filterName);
        //if (ich === -1) {
        //    href += '?filter=advancedsearch-.-.-' + txt;
        //} else {
        //    var filter = href.substring(ich + filterName.length);
        //    var params = filter.split('-');
        //    for (var i = 0; i < 3; i++) {
        //        if (!params[i]) params[i] = '.';
        //    }
        //    params[3] = txt;
        //    filter = params.join('-');
        //    href = href.substring(0, ich + filterName.length) + filter;
        //}

        $(this).attr('href', href);
    });

    $('#search').submit(function (event) { filter.update(event); });
    $('#reset-search').click(function () { filter.reset(event); });

    filter.init();
});

