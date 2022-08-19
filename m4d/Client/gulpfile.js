/* eslint-disable @typescript-eslint/no-var-requires */
var gulp = require("gulp"),
    cleanCss = require("gulp-clean-css"),
    sass = require("gulp-dart-sass"),
    autoprefixer = require("gulp-autoprefixer"),
    merge = require("merge2");

function bootstrap4() {
    return gulp
        .src("./styles/bootstrap/*.scss")
        .pipe(
            sass({
                includePaths: [
                    "./node_modules/bootstrap/scss",
                    "./node_modules/@fortawesome/fontawesome-free/scss",
                ],
            })
        )
        .pipe(autoprefixer())
        .pipe(cleanCss())
        .pipe(gulp.dest(rootPath("css")));
}

// function watch4() {
//   return gulp.watch(
//     webPath("Styles/bootstrap4/**/*.scss"),
//     gulp.series(["bootstrap4"])
//   );
// }

function fontawesome() {
    return gulp
        .src("./node_modules/@fortawesome/fontawesome-free/webfonts/*.*")
        .pipe(gulp.dest(rootPath("fonts/")));
}

function authButtons() {
    return gulp
        .src("./styles/auth-buttons/*.*")
        .pipe(gulp.dest(rootPath("css/")));
}

function dances() {
    return gulp
        .src("../../DanceLib/*.txt")
        .pipe(gulp.dest(rootPath("content/")));
}

function assets() {
    return gulp.src("./assets/**/*").pipe(gulp.dest(rootPath("")));
}

var deps = {
    bootstrap: {
        "dist/js/*": "",
    },
    jquery: {
        "dist/*": "",
    },
    "jquery-validation": {
        "dist/*": "",
    },
    markdowndeep: {
        "clientSide/*": "",
    },
};

function scripts() {
    var streams = [];

    for (var prop in deps) {
        console.log("Prepping Scripts for: " + prop);
        for (var itemProp in deps[prop]) {
            streams.push(
                gulp
                    .src("node_modules/" + prop + "/" + itemProp)
                    .pipe(
                        gulp.dest(
                            rootPath(
                                "vendor/" + prop + "/" + deps[prop][itemProp]
                            )
                        )
                    )
            );
        }
    }

    return merge(streams);
}

function rootPath(fragment) {
    return `../wwwroot/${fragment}`;
}

exports.bootstrap = bootstrap4;
exports.fontawesome = fontawesome;
exports.authButtons = authButtons;
exports.dances = dances;
exports.assets = assets;
exports.scripts = scripts;
exports.build = gulp.parallel(
    bootstrap4,
    fontawesome,
    authButtons,
    dances,
    assets,
    scripts
);
