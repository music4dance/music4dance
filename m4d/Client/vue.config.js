// vue.config.js
module.exports = {
    outputDir: "../wwwroot/client/",
    filenameHashing: false,
    pages: {
      "advanced-search": {
        entry: "src/pages/advanced-search/main.ts",
        filename: "advanced-search.html"
      },
      "album": {
        entry: "src/pages/album/main.ts",
        filename: "album.html"
      },
      "artist": {
        entry: "src/pages/artist/main.ts",
        filename: "artist.html"
      },
      "ballroom-index": {
        entry: "src/pages/ballroom-index/main.ts",
        filename: "ballroom-index.html"
      },
      "competition-category": {
        entry: "src/pages/competition-category/main.ts",
        filename: "competition-category.html"
      },
      "dance-index": {
        entry: "src/pages/dance-index/main.ts",
        filename: "dance-index.html"
      },
      "faq": {
        entry: "src/pages/faq/main.ts",
        filename: "faq.html"
      },
      "home": {
        entry: "src/pages/home/main.ts",
        filename: "home.html"
      },
      "new-music": {
        entry: "src/pages/new-music/main.ts",
        filename: "new-music.html"
      },
      "song-index": {
        entry: "src/pages/song-index/main.ts",
        filename: "song-index.html"
      },
      "tag-index": {
        entry: "src/pages/tag-index/main.ts",
        filename: "tag-index.html"
      },
      "tempo-counter": {
        entry: "src/pages/tempo-counter/main.ts",
        filename: "tempo-counter.html"
      },
      "tempo-list": {
        entry: "src/pages/tempo-list/main.ts",
        filename: "tempo-list.html"
      },
      "wedding-dance-music": {
        entry: "src/pages/wedding-dance-music/main.ts",
        filename: "wedding-dance-music.html"
      },
    },
    configureWebpack: {
      "devtool": "source-map"
    }
  }