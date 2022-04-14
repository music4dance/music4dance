// vue.config.js
module.exports = {
    outputDir: "../wwwroot/client/",
    filenameHashing: false,
    pages: {
        about: {
            entry: "src/pages/about/main.ts",
        },
        "advanced-search": {
            entry: "src/pages/advanced-search/main.ts",
        },
        album: {
            entry: "src/pages/album/main.ts",
        },
        artist: {
            entry: "src/pages/artist/main.ts",
        },
        augment: {
            entry: "src/pages/augment/main.ts",
        },
        "ballroom-index": {
            entry: "src/pages/ballroom-index/main.ts",
        },
        "competition-category": {
            entry: "src/pages/competition-category/main.ts",
        },
        "dance-details": {
            entry: "src/pages/dance-details/main.ts",
        },
        "dance-index": {
            entry: "src/pages/dance-index/main.ts",
        },
        faq: {
            entry: "src/pages/faq/main.ts",
        },
        header: {
            entry: "src/pages/header/main.ts",
        },
        "holiday-music": {
            entry: "src/pages/holiday-music/main.ts",
        },
        home: {
            entry: "src/pages/home/main.ts",
        },
        "new-music": {
            entry: "src/pages/new-music/main.ts",
        },
        resume: {
            entry: "src/pages/resume/main.ts",
        },
        search: {
            entry: "src/pages/search/main.ts",
        },
        song: {
            entry: "src/pages/song/main.ts",
        },
        "song-index": {
            entry: "src/pages/song-index/main.ts",
        },
        "tag-index": {
            entry: "src/pages/tag-index/main.ts",
        },
        "tempo-counter": {
            entry: "src/pages/tempo-counter/main.ts",
        },
        "tempo-list": {
            entry: "src/pages/tempo-list/main.ts",
        },
        "wedding-dance-music": {
            entry: "src/pages/wedding-dance-music/main.ts",
        },
        "user-info": {
            entry: "src/pages/user-info/main.ts",
        },
    },
    configureWebpack: (config) => {
        if (process.env.NODE_ENV === "production") {
            config.output.filename = "js/[name].prod001.js";
            config.output.chunkFilename = "js/[name].prod001.js";
        } else {
            config.output.filename = "js/[name].local.js";
            config.output.chunkFilename = "js/[name].local.js";
            config.devtool = "source-map";
        }
    },
};
