/// <binding BeforeBuild='styles, scripts, fonts' />

var gulp = require("gulp"),
    cleanCss = require("gulp-clean-css"),
    less = require("gulp-less"),
    merge = require('merge2');

gulp.task("styles", function () {
    return gulp.src('Styles/*.less')
        .pipe(less({ paths: ['./node_modules/bootstrap-less/bootstrap'] }))
        .pipe(cleanCss())
        .pipe(gulp.dest('wwwroot/css'));
});

gulp.task('fonts', function () {
    return gulp.src('./node_modules/bootstrap-less/fonts/glyphicons-halflings-regular.*')
        .pipe(gulp.dest('wwwroot/fonts/'));
});

var deps = {
    "bootstrap3": {
        "dist/**/*": ""
    },
    "jquery": {
        "dist/*": ""
    },
    "jquery-validation": {
        "dist/*": ""
    },
    "knockout": {
        "build/output/*": ""
    },
    "knockout-mapping": {
        "dist/*": ""
    }
};

gulp.task("scripts", function () {

    var streams = [];

    for (var prop in deps) {
        console.log("Prepping Scripts for: " + prop);
        for (var itemProp in deps[prop]) {
            streams.push(gulp.src("node_modules/" + prop + "/" + itemProp)
                .pipe(gulp.dest("wwwroot/vendor/" + prop + "/" + deps[prop][itemProp])));
        }
    }

    return merge(streams);
});
