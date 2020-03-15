/// <binding BeforeBuild='styles' />

var gulp = require("gulp"),
    cleanCss = require("gulp-clean-css"),
    less = require("gulp-less");

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

