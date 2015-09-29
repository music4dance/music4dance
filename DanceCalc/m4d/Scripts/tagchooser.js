// Not ready to try the equivalent of an interface in JS:  However will do so by convention here
// tagggedObject is used as such & must implement:
//    CurrentUserTags
//    TagSummary
//    changed = ko.observable(bool)
//    changeText = function() {return test}
//    Optional DanceId
//    Optional extraTagTypes = [{name,label}]
// $root (viewModel) must implement
//    canTag = ko.observable(bool)
//    showTag = ko.objervable(bool)

var tagChooser = function () {
    var categoryToIcon = function (cat) {
        var classMap = { 'style': 'dance', 'tempo': 'tempo', 'music': 'genre' };

        var cls = 'tag';
        var key = cat.toLowerCase();
        if (classMap.hasOwnProperty(key)) {
            cls = classMap[key];
        }
        return '/content/' + cls + '-50.png';
    }

    //  TagSuggestion Object
    // ReSharper disable once InconsistentNaming
    var TagSuggestion = function (tag, count) {
        var self = this;

        self.value = tag.split(':', 1)[0];
        self.count = count;
    };

    //  CurrentSuggestions Object
    // ReSharper disable once InconsistentNaming
    var CurrentSuggestions = function (title) {
        var self = this;

        self.title = ko.observable(title);
        self.type = ko.observable('none');
        self.chosen = ko.observableArray();
        self.user = ko.observableArray();
        self.popular = ko.observableArray();
        self.all = ko.observableArray();
        self.userSort = ko.observable('weight');
        self.popularSort = ko.observable('weight');

        self.typeClass = ko.pureComputed(function () {
            return 'text-' + self.type().toLowerCase();
        });

        self.hrClass = ko.pureComputed(function () {
            return 'hr-' + self.type().toLowerCase();
        });

        self.sortButton = function (type, order) {
            var button = (type === 'user') ? self.userSort : self.popularSort;

            var ret = 'btn-' + (order === button() ? self.type().toLowerCase() : 'default');
            return ret;
        }

        self.sortBy = function (type, order) {
            var button = (type === 'user') ? self.userSort : self.popularSort;
            if (button() === order) {
                return;
            }

            var list = (type === 'user') ? self.user : self.popular;

            if (order === 'alpha') {
                list.sort(function (left, right) { return left.value === right.value ? 0 : (left.value < right.value ? -1 : 1); });
            } else {
                list.sort(function (left, right) { return right.count - left.count });
            }
            button(order);
        }

        self.findSuggestion = function (val) {
            var rg = self.all();
            for (var idx = 0; idx < rg.length; idx++) {
                var ret = rg[idx];
                if (ret.value === val)
                    return ret;
            }
            return null;
        }

        self.addTag = function (val) {
            var tag = self.findSuggestion(val);
            self.chosen.push(tag === null ? { value: val } : tag);
            $('#chosen').trigger('chosen:updated');
        }

        self.addTagLink = function (link) {
            self.addTag(link.value);
        }

        self.removeTag = function (val) {
            var tag = findSuggestion(val);
            if (tag !== null) {
                self.chosen.remove(tag);
                $('#chosen').trigger('chosen:updated');
            }
        }
    };

    //  TagSuggestions Object
    // ReSharper disable once InconsistentNaming
    var TagSuggestions = function (help, contexts) {
        var self = this;

        self.help = ko.observable(help);
        self.music = { user: null, popular: null };
        self.style = { user: null, popular: null };
        self.tempo = { user: null, popular: null };
        self.other = { user: null, popular: null };

        self.current = ko.observable(null);

        self.contexts = contexts;
        if ($.isArray(self.contexts)) {
            self.current(context[0]);
        } else {
            self.current(contexts);
            self.contexts = [contexts];
        }

        self.massageTags = function (data, array) {
            array.removeAll();
            if (data != null) {
                for (var i = 0; i < data.length; i++) {
                    var ts = new TagSuggestion(data[i].Value, data[i].Count);
                    array.push(ts);
                    if (!self.current().findSuggestion(ts.value)) {
                        self.current().all.push(ts);
                    }
                }
            }
            self.current().all().sort(function (left, right) { return left.value === right.value ? 0 : (left.value < right.value ? -1 : 1); });
        };

        self.getSuggestions = function (obj, type, user) {
            var kind = user ? 'user' : 'popular';

            var uri = '/api/tagsuggestion?tagType=' + type;
            if (user) {
                uri += '&count=500&user=' + user;
            }
            else {
                uri += '&normalized=true&count=500';
            }

            var msg = $('#' + kind + '-message');
            var lst = $('#' + kind + '-list');

            msg.show();
            lst.hide();

            $.getJSON(uri)
                .done(function (data) {
                    var sug = self.sugFromType(type);
                    msg.hide();
                    lst.show();
                    if (user) {
                        sug.user = data;
                        self.massageTags(data, self.current().user);
                    } else {
                        sug.popular = data;
                        self.massageTags(data, self.current().popular);
                    }
                    self.updateChosen(obj, type);
                    //window.alert('type=' + type + 'data=' + JSON.stringify(data));
                })
                .fail(function (jqXhr /*, textStatus ,err*/) {
                    var message = "Server Error: " + jqXhr.status + " - " + jqXhr.statusText;
                    console.log(message);
                    var msgt = msg.find('p');
                    msgt.text(message);
                });
        };

        self.sugFromType = function (type) {
            return self[type.toLowerCase()];
        };

        self.updateChosen = function (obj, type) {
            var chosen = obj.TagSummary.getUserTags(type);
            self.current().chosen.removeAll();
            for (var i = 0; i < chosen.length; i++) {
                self.current().addTag(chosen[i]);
            }
            $('#chosen').trigger('chosen:updated');
        }

        self.setSuggestions = function (obj, type) {
            var sug = self.sugFromType(type);

            self.current().chosen.removeAll();
            var deferred = false;

            if (self.current().type() !== type) {
                self.current().type(type);
                self.current().all.removeAll();

                if (sug.user === null) {
                    deferred = true;
                    self.getSuggestions(obj, type, window.userId);
                } else {
                    self.massageTags(sug.user, self.current().user);
                }

                if (sug.popular === null) {
                    self.getSuggestions(obj, type);
                } else {
                    self.massageTags(sug.popular, self.current().popular);
                }
            }

            if (!deferred) self.updateChosen(obj, type);
        }

        self.updateSuggestions = function (newTags) {
            var sug = self.sugFromType(self.current().type());
            for (var i = 0; i < newTags.length; i++) {
                var name = newTags[i];
                sug.user.push({ Value: name, Count: 1 });
                var t = new TagSuggestion(name, 1);
                self.current().user.push(t);
                self.current().all.push(t);
            }
        }
    };

    // ReSharper disable once InconsistentNaming
    var Tag = function (value, taggedObject) {
        var self = this;

        var values = value.split(':');

        self.tag = ko.observable(values.length > 0 ? values[0] : null);
        self.cat = ko.observable(values.length > 1 ? values[1] : null);
        self.cnt = ko.observable(values.length > 2 ? values[2] : 1);

        self.taggedObject = taggedObject;

        self.userTags = ko.pureComputed(function () {
            return self.taggedObject.CurrentUserTags;
        });

        self.value = ko.pureComputed(function () {
            return self.tag() + ':' + self.cat();
        }, this);

        self.url = ko.pureComputed(function () {
            return '/song/tags?tags=' + encodeURIComponent(self.tag()) + ':' + encodeURIComponent(self.cat());
        }, this);

        self.urlNot = ko.pureComputed(function () {
            return '/song/tags?tags=-' + encodeURIComponent(self.tag()) + ':' + encodeURIComponent(self.cat());
        }, this);

        self.imageSrc = ko.pureComputed(function () {
            return categoryToIcon(self.cat());
        }, this);

        self.isUserTag = ko.pureComputed(function () {
            var val = self.tag() + ':' + self.cat();
            return self.userTags().Tags.indexOf(val) !== -1;
        }, this);

        self.tagClass = ko.pureComputed(function () {
            var ret = self.isUserTag() ? 'glyphicon-tag' : 'glyphicon-plus-sign';
            return ret;
        }, this);

        self.toggleText = ko.pureComputed(function () {
            return (self.isUserTag() ? 'Remove "' + self.tag() + '" from ' : 'Add "' + self.tag() + '" to ') + taggedObject.changeText();
        }, this);

        self.toggleUser = function () {
            var value = self.value();
            var count = self.cnt();
            if (self.isUserTag()) {
                self.userTags().Tags.remove(value);
                count -= 1;
            } else {
                self.userTags().Tags.push(value);
                count += 1;
            }
            self.cnt(count);
            if (count === 0) {
                taggedObject.TagSummary.Tags.remove(self);
            }

            taggedObject.changed(true);
        };
    };

    // ReSharper disable once InconsistentNaming
    var TagType = function (summary, name, label) {
        var self = this;

        self.summary = summary;
        self.name = name;
        self.label = label;

        self.list = ko.computed(function () {
            return ko.utils.arrayFilter(self.summary.Tags(), function (tag) {
                return tag.cat() === self.name;
            });
        }, this);

        self.tooltip = ko.pureComputed(function () {
            return 'Add or Change ' + self.label + ' tags.';
        }, this);

        self.imageSrc = ko.pureComputed(function () {
            return categoryToIcon(name);
        }, this);
        self.nameLower = function () { return name.toLowerCase() };
    };

    // Tag Summary Object
    // ReSharper disable once InconsistentNaming
    var TagSummary = function (data, taggedObject, forSong) {
        var self = this;
        self.taggedObject = taggedObject;

        self.tagsFromSummary = function (summary) {
            if (!summary) return [];

            var list = [];
            var tcs = summary.split('|');
            for (var i = 0; i < tcs.length; i++) {
                list.push(new Tag(tcs[i].trim(), self.taggedObject));
            }
            return list;
        };

        self.Summary = ko.observable(data.Summary);
        self.Tags = ko.observableArray(self.tagsFromSummary(data.Summary));
        self.userTags = ko.pureComputed(function () {
            return self.taggedObject.CurrentUserTags;
        }, this);

        // Build the tag types
        self.tagTypes = [];
        if (taggedObject.extraTagTypes) {
            for (var i = 0; i < taggedObject.extraTagTypes.length; i++) {
                self.tagTypes.push(new TagType(self, taggedObject.extraTagTypes[i].name, taggedObject.extraTagTypes[i].label));
            }
        }
        self.tagTypes.push(new TagType(self, 'Tempo', 'Tempo'));
        self.tagTypes.push(new TagType(self, 'Other', 'Other'));

        self.danceId = ko.pureComputed(function () {
            if (self.taggedObject.DancId)
                return self.taggedObject.DanceId();
            else
                return '';
        });

        self.hasTag = function (value, kind) {
            var s = value + ':' + kind;
            for (var i = 0; i < self.Tags().length; i++) {
                if (self.Tags()[i].value() === s) {
                    return true;
                }
            }
            return false;
        };

        self.getTagType = function (tt) {
            for (var i = 0; i < self.tagTypes.length; i++) {
                if (self.tagTypes[i].name === tt)
                    return self.tagTypes[i];
            }
            return null;
        };

        self.findTag = function (tag, cat) {
            var tagO = null;
            for (var i = 0; i < self.Tags().length; i++) {
                var tagT = self.Tags()[i];

                if (tagT.tag() === tag && tagT.cat() === cat) {
                    tagO = tagT;
                    break;
                }
            }
            return tagO;
        };

        self.findUserTag = function (tag, cat) {
            var tagO = self.findTag(tag, cat);
            return (tagO && tagO.isUserTag()) ? tagO : null;
        };

        self.removeTag = function (tag, cat) {
            // Is there an existing match?
            var tagO = self.findTag(tag, cat);

            // If there isn't an existing tag, we're done
            if (!tagO) return;

            self.userTags().Tags.remove(tag + ':' + cat);
            var cnt = tagO.cnt() - 1;
            if (cnt <= 0) {
                self.Tags.remove(tagO);
            }
            else {
                tagO.cnt(cnt);
            }

            taggedObject.changed(true);
        };

        self.addTag = function (tag, cat) {
            // Is there an existing match?
            var tagO = self.findTag(tag, cat);

            // If there isn't an existing tag, create it
            var value = tag + ':' + cat;
            if (!tagO) {
                tagO = new Tag(value, self.taggedObject);
                self.Tags.push(tagO);
            } else {
                tagO.cnt(tagO.cnt() + 1);
            }

            // Add tags to the associated userTag list if needed
            if (!tagO.isUserTag()) {
                self.userTags().Tags.push(value);
            }

            taggedObject.changed(true);
        };

        self.getUserTags = function (type) {
            var ret = [];
            var tags = self.userTags().Tags();
            for (var i = 0; i < tags.length; i++) {
                var tag = tags[i];
                var info = tag.split(':');
                if (info.length > 1 && info[1] === type) {
                    ret.push(info[0]);
                }
            }
            return ret;
        }

        self.changeTags = function (tags, category) {
            // First remove user tags that aren't in the new list
            var oldTags = self.getUserTags(category);
            for (var i = 0; i < oldTags.length; i++) {
                if (tags.indexOf(oldTags[i]) === -1) {
                    self.removeTag(oldTags[i], category);
                }
            }

            // Then add the new tags: Tags that were created by the chosen addobject feature
            //  are strings, all others are objects.  Use this fact to return an array of
            //  new tags to be added to the suggestions list.
            var newTags = [];
            for (var j = 0; j < tags.length; j++) {
                var obj = tags[j];

                if ($.type(obj) === 'string') {
                    newTags.push(obj);
                }
                else {
                    obj = obj.value;
                }

                self.addTag(obj, category);
            }

            return newTags;
        }

        self.serializeUser = function () {
            var s = '';
            var sep = '';
            for (var i = 0; i < self.userTags().Tags().length; i++) {
                var tag = self.userTags().Tags()[i];
                s += sep + tag;
                sep = '|';
            }
            return s;
        };
    };

    var tagSummary = function (data, taggedObject) {
        return new TagSummary(data, taggedObject);
    };

    var currentSuggestion = function (title) {
        return new CurrentSuggestions(title);
    };

    var tagSuggestions = function (help, contexts) {
        return new TagSuggestions(help, contexts);
    };

    return {
        tagSummary: tagSummary,
        currentSuggestion: currentSuggestion,
        tagSuggestions: tagSuggestions
    };
}();
