// vue.config.js
module.exports = {
    outputDir: "../wwwroot/client/",
    filenameHashing: false,
    pages: {
      "advanced-search": {
        entry: "src/advanced-search/main.ts",
        filename: "advanced-search.html"
      },
      "ballroom-index": {
        entry: "src/ballroom-index/main.ts",
        filename: "ballroom-index.html"
      },
      "competition-category": {
        entry: "src/competition-category/main.ts",
        filename: "competition-category.html"
      },
      "dance-index": {
        entry: "src/dance-index/main.ts",
        filename: "dance-index.html"
      },
      "faq": {
        entry: "src/faq/main.ts",
        filename: "faq.html"
      },
      "home": {
        entry: "src/home/main.ts",
        filename: "home.html"
      },
      "song-index": {
        entry: "src/song-index/main.ts",
        filename: "songi-index.html"
      },
      "tag-index": {
        entry: "src/tag-index/main.ts",
        filename: "tag-index.html"
      },
      "tempo-counter": {
        entry: "src/tempo-counter/main.ts",
        filename: "tempo-counter.html"
      },
      "tempo-list": {
        entry: "src/tempo-list/main.ts",
        filename: "tempo-list.html"
      },
      "wedding-dance-music": {
        entry: "src/wedding-dance-music/main.ts",
        filename: "wedding-dance-music.html"
      },
    },
    configureWebpack: {
      "devtool": "source-map"
    }
  }