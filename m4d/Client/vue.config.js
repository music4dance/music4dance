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
      "faq": {
        entry: "src/faq/main.ts",
        filename: "faq.html"
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