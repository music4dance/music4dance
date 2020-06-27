/// <binding BeforeBuild='scripts, glyphicons, bootstrap4, bootstrap3, fontawesome' />

var gulp = require("gulp"),
    cleanCss = require("gulp-clean-css"),
    less = require("gulp-less"),
    sass = require("gulp-sass"),
    autoprfixer = require("gulp-autoprefixer"),
    merge = require('merge2');

gulp.task("bootstrap3", function () {
    return gulp.src('Styles/bootstrap3/*.less')
        .pipe(less({ paths: ['./node_modules/bootstrap-less/bootstrap'] }))
        .pipe(cleanCss())
        .pipe(gulp.dest('wwwroot/css'));
});

gulp.task("bootstrap4", function () {
    return gulp.src('Styles/bootstrap4/*.scss')
        .pipe(sass({ includePaths: ['./node_modules/bootstrap/scss', './node_modules/@fortawesome/fontawesome-free/scss'] }))
        .pipe(autoprfixer())
        .pipe(cleanCss())
        .pipe(gulp.dest('wwwroot/css'));
});

gulp.task('watch3', function () {
    return gulp.watch('Styles/bootstrap3/**/*.less', gulp.series(['bootstrap3']));
});

gulp.task('watch4', function () {
    return gulp.watch('Styles/bootstrap4/**/*.scss', gulp.series(['bootstrap4']));
});

gulp.task('glyphicons', function () {
    return gulp.src('./node_modules/bootstrap-less/fonts/glyphicons-halflings-regular.*')
        .pipe(gulp.dest('wwwroot/fonts/'));
});

gulp.task('fontawesome', function () {
    return gulp.src('./node_modules/@fortawesome/fontawesome-free/webfonts/*.*')
        .pipe(gulp.dest('wwwroot/fonts/'));
});

var deps = {
    "bootstrap3": {
        "dist/**/*": ""
    },
    "bootstrap": {
        "dist/js/*":""
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
    },
    "markdowndeep": {
        "clientSide/*": ""
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
