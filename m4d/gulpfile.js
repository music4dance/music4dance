/// <binding BeforeBuild='scripts, auth-buttons, bootstrap4, fontawesome' />

var gulp = require("gulp"),
    cleanCss = require("gulp-clean-css"),
    sass = require("gulp-sass"),
    autoprefixer = require("gulp-autoprefixer"),
    merge = require("merge2");

gulp.task("bootstrap4",
    function() {
        return gulp.src("Styles/bootstrap4/*.scss")
            .pipe(sass({
                includePaths: ["./node_modules/bootstrap/scss", "./node_modules/@fortawesome/fontawesome-free/scss"]
            }))
            .pipe(autoprefixer())
            .pipe(cleanCss())
            .pipe(gulp.dest("wwwroot/css"));
    });

gulp.task("watch4",
    function() {
        return gulp.watch("Styles/bootstrap4/**/*.scss", gulp.series(["bootstrap4"]));
    });

gulp.task("fontawesome",
    function() {
        return gulp.src("./node_modules/@fortawesome/fontawesome-free/webfonts/*.*")
            .pipe(gulp.dest("wwwroot/fonts/"));
    });

gulp.task("auth-buttons",
    function() {
        return gulp.src("Styles/auth-buttons/*.*")
            .pipe(gulp.dest("wwwroot/css/"));
    });

var deps = {
    "bootstrap": {
        "dist/js/*": ""
    },
    "jquery": {
        "dist/*": ""
    },
    "jquery-validation": {
        "dist/*": ""
    },
    "markdowndeep": {
        "clientSide/*": ""
    }
};

gulp.task("scripts",
    function() {

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