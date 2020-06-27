// vue.config.js
module.exports = {
    outputDir: "../wwwroot/client/",
    filenameHashing: false,
    pages: {
      "advanced-search": {
        entry: "src/advanced-search/main.ts",
        filename: "advanced-search.html"
      },
      "tempo-counter": {
        entry: "src/tempo-counter/main.ts",
        filename: "tempo-counter.html"
      },
      "tempo-list": {
        entry: "src/tempo-list/main.ts",
        filename: "tempo-list.html"
      }
    },
    configureWebpack: {
      "devtool": "source-map"
    }
  }