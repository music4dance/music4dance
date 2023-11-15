<script setup lang="ts">
import PageFrame from "@/components/PageFrame.vue";
import { type BreadCrumbItem, infoTrail } from "@/models/BreadCrumbItem";
import { BookCategory } from "./models/BookCategory";
import type { Book } from "./models/BookModel";
import { BookType } from "./models/BookType";
import ReadingCategory from "./components/ReadingCategory.vue";
import { computed } from "vue";

//  TODO: Consider pushing the Book list down from the server
const books: Book[] = [
  {
    type: BookType.Education,
    title: "Hear the Beat, Feel the Music",
    author: "James Joseph",
    kindle: "https://amzn.to/2ZKMWRS",
    paperback: "https://amzn.to/2zH4uDQ",
    review: "https://music4dance.blog/2019/08/24/hear-the-beat-feel-the-music/",
    website: {
      text: "ihatetodance.com",
      ref: "https://ihatetodance.com/",
    },
    notes:
      "This is a great read for those dancers that don’t have a musical background and are struggling with “musicality.”",
    image: `<a href="https://www.amazon.com/Hear-Beat-Feel-Music-Remarkable-ebook/dp/B07DPVFY6V?crid=1MIHB6FI75J8D&keywords=hear+the+beat+feel+the+music&qid=1566677512&s=gateway&sprefix=hear+the+music%2C+feel%2Caps%2C202&sr=8-1&linkCode=li2&tag=msc4dnc-20&linkId=9018732b1d509231e2a4a75b3ba9835f&language=en_US&ref_=as_li_ss_il" target="_blank"><img border="0" src="//ws-na.amazon-adsystem.com/widgets/q?_encoding=UTF8&ASIN=B07DPVFY6V&Format=_SL160_&ID=AsinImage&MarketPlace=US&ServiceVersion=20070822&WS=1&tag=msc4dnc-20&language=en_US" ></a><img src="https://ir-na.amazon-adsystem.com/e/ir?t=msc4dnc-20&language=en_US&l=li2&o=1&a=B07DPVFY6V" width="1" height="1" border="0" alt="" style="border:none !important; margin:0px !important;" />`,
  },
  {
    type: BookType.Education,
    title: "Every Man's Survival Guide to Ballroom Dancing",
    subtitle:
      "Ace Your Wedding Dance and Keep Cool on a Cruise, at a Formal, and in Dance Classes Paperback",
    author: "James Joseph",
    kindle: "https://amzn.to/47j07NO",
    paperback: "https://amzn.to/3ugpKA4",
    review: "https://music4dance.blog/2016/10/31/feel-the-beat/",
    website: {
      text: "ihatetodance.com",
      ref: "https://ihatetodance.com/",
    },
    notes:
      "This is the book I wish I had when I first started dancing as a somewhat socially awkward 20 something.  This book lays out some solid advice about how to behave on the dance floor and general etiquitte in various dance environments on top of instructions on how to dance to music.",
    image: `<a href="https://www.amazon.com/Every-Survival-Guide-Ballroom-Dancing-ebook/dp/B007MPUSAI?_encoding=UTF8&qid=1477934551&sr=8-1&linkCode=li2&tag=msc4dnc-20&linkId=2d6bdcb5ad34244ae733daaa6e5592b6&language=en_US&ref_=as_li_ss_il" target="_blank"><img border="0" src="//ws-na.amazon-adsystem.com/widgets/q?_encoding=UTF8&ASIN=B007MPUSAI&Format=_SL160_&ID=AsinImage&MarketPlace=US&ServiceVersion=20070822&WS=1&tag=msc4dnc-20&language=en_US" ></a><img src="https://ir-na.amazon-adsystem.com/e/ir?t=msc4dnc-20&language=en_US&l=li2&o=1&a=B007MPUSAI" width="1" height="1" border="0" alt="" style="border:none !important; margin:0px !important;" />`,
  },
  {
    type: BookType.Education,
    title: "Partner Dance Success (Volume 1)",
    subtitle: "Be the One They Want - What I Wish I Knew When I Started Social Dancing",
    author: "Don Baarns",
    kindle: "https://amzn.to/2SVUQKY",
    paperback: "https://amzn.to/35Z2Xtj",
    review: "https://music4dance.blog/2021/06/22/book-review-partner-dance-success/",
    notes: "This is a great collection of practical advice for anyone new to partner dancing.",
    image: `<a href="https://www.amazon.com/Partner-Dance-Success-Started-Dancing-ebook/dp/B00CLG16QI?_encoding=UTF8&qid=1624407627&sr=8-1&linkCode=li2&tag=msc4dnc-20&linkId=46f044000d9445f083bc3814d8193758&language=en_US&ref_=as_li_ss_il" target="_blank"><img border="0" src="//ws-na.amazon-adsystem.com/widgets/q?_encoding=UTF8&ASIN=B00CLG16QI&Format=_SL160_&ID=AsinImage&MarketPlace=US&ServiceVersion=20070822&WS=1&tag=msc4dnc-20&language=en_US" ></a><img src="https://ir-na.amazon-adsystem.com/e/ir?t=msc4dnc-20&language=en_US&l=li2&o=1&a=B00CLG16QI" width="1" height="1" border="0" alt="" style="border:none !important; margin:0px !important;" />`,
  },
  {
    type: BookType.Education,
    title: "Partner Dance Success (Volume 2)",
    subtitle: "Be the One They Want - What I Wish I Knew When I Started Social Dancing",
    author: "Don Baarns",
    kindle: "https://amzn.to/3xJDSyd",
    paperback: "https://amzn.to/3d4QGai",
    review: "https://music4dance.blog/2021/06/22/book-review-partner-dance-success/",
    notes: "This is a great collection of practical advice for anyone new to partner dancing.",
    image: `<a href="https://www.amazon.com/Partner-Dance-Success-Started-Dancing-ebook/dp/B00DVQ7WCE?dchild=1&keywords=Partner+Dance+for+Success&qid=1624406279&sr=8-3&linkCode=li2&tag=msc4dnc-20&linkId=2f50a0fa5a2463cf57fab89288e02528&language=en_US&ref_=as_li_ss_il" target="_blank"><img border="0" src="//ws-na.amazon-adsystem.com/widgets/q?_encoding=UTF8&ASIN=B00DVQ7WCE&Format=_SL160_&ID=AsinImage&MarketPlace=US&ServiceVersion=20070822&WS=1&tag=msc4dnc-20&language=en_US" ></a><img src="https://ir-na.amazon-adsystem.com/e/ir?t=msc4dnc-20&language=en_US&l=li2&o=1&a=B00DVQ7WCE" width="1" height="1" border="0" alt="" style="border:none !important; margin:0px !important;" />`,
  },
  {
    type: BookType.Education,
    title: "Dance Better Now",
    subtitle: "The secret to mastering dance and dancing at the excellent standard you want by",
    author: "Clint Steele",
    kindle: "https://amzn.to/2Lfq7Sx",
    notes: "This books is an educator’s take on how to rapidly improve your level of social dance.",
    image: `<a href="https://www.amazon.com/Dance-Better-Now-mastering-excellent-ebook/dp/B010T578GE?ie=UTF8&linkCode=li2&tag=msc4dnc-20&linkId=f41b0c5f8b7a7da8d2584dd069588b04&language=en_US&ref_=as_li_ss_il" target="_blank"><img border="0" src="//ws-na.amazon-adsystem.com/widgets/q?_encoding=UTF8&ASIN=B010T578GE&Format=_SL160_&ID=AsinImage&MarketPlace=US&ServiceVersion=20070822&WS=1&tag=msc4dnc-20&language=en_US" ></a><img src="https://ir-na.amazon-adsystem.com/e/ir?t=msc4dnc-20&language=en_US&l=li2&o=1&a=B010T578GE" width="1" height="1" border="0" alt="" style="border:none !important; margin:0px !important;" />`,
  },
  {
    type: [BookType.Education],
    title: "How to Read Music in 30 Days",
    subtitle:
      "Music Theory for Beginners - with Exercises & Online Audio (Practical Music Theory Book 1)",
    author: "Matthew Ellul",
    kindle: "https://amzn.to/3CUlzw9",
    paperback: "https://amzn.to/3cHbEiU",
    hardcover: "https://amzn.to/3Qb0HUg",
    others: [
      {
        text: "Music Theory for Beginners (spiral-bound)",
        ref: "https://amzn.to/3RxIv93",
      },
    ],
    notes:
      "This book is a quick read that does a credible job of covering the basics of reading music.  For dancers that are interested in checking sheet music for tempo information, the first half of the book covers time signatures and tempo markings.",
    review: "",
    image: `<a href="https://www.amazon.com/How-Read-Music-Days-Beginners-ebook/dp/B08KD4XZFK?_encoding=UTF8&qid=&sr=&linkCode=li2&tag=msc4dnc-20&linkId=2fe5bcf1cada5e84ef112707a383de29&language=en_US&ref_=as_li_ss_il" target="_blank"><img border="0" src="//ws-na.amazon-adsystem.com/widgets/q?_encoding=UTF8&ASIN=B08KD4XZFK&Format=_SL160_&ID=AsinImage&MarketPlace=US&ServiceVersion=20070822&WS=1&tag=msc4dnc-20&language=en_US" ></a><img src="https://ir-na.amazon-adsystem.com/e/ir?t=msc4dnc-20&language=en_US&l=li2&o=1&a=B08KD4XZFK" width="1" height="1" border="0" alt="" style="border:none !important; margin:0px !important;" />`,
  },
  {
    type: [BookType.History, BookType.Education],
    title: "The Meaning of Tango",
    subtitle: "The Story of the Argentinian Dance",
    author: "Christine Denniston",
    kindle: "https://amzn.to/2NKZTZH",
    paperback: "https://amzn.to/2zLAMOm",
    hardcover: "https://amzn.to/2ZK1D83",
    notes:
      "This book is a quick read and full of wonderful tidbits about Tango and its history. The book is very centered around traditional Argentine Tango and does an excellent job of conveying that perspective.  It’s also somewhat unusual in that it is predominantly about the history and philosophy of the dance but contains a section that is straight up technique with diagrams",
    review: "https://music4dance.blog/2018/07/21/book-review-the-meaning-of-tango/",
    image: `<a href="https://www.amazon.com/Meaning-Tango-Story-Argentinian-Dance/dp/1906032165?_encoding=UTF8&qid=1567446252&sr=8-1&linkCode=li2&tag=msc4dnc-20&linkId=ccad93ab5e6f994d928653dc0bf23ff3&language=en_US&ref_=as_li_ss_il" target="_blank"><img border="0" src="//ws-na.amazon-adsystem.com/widgets/q?_encoding=UTF8&ASIN=1906032165&Format=_SL160_&ID=AsinImage&MarketPlace=US&ServiceVersion=20070822&WS=1&tag=msc4dnc-20&language=en_US" ></a><img src="https://ir-na.amazon-adsystem.com/e/ir?t=msc4dnc-20&language=en_US&l=li2&o=1&a=1906032165" width="1" height="1" border="0" alt="" style="border:none !important; margin:0px !important;" />`,
  },
  {
    type: BookType.History,
    title: "A Century of Dance",
    subtitle: "A Hundred Years of Musical Movement, from Waltz to Hip Hop",
    author: "Ian Driver",
    paperback: "https://amzn.to/3MamSIx",
    notes:
      "This is a large format book with many amazing illustrations.  But the best part of this books from my perspective is that it gives an easy to read overview of the history of dance in the Twentieth Century, giving some useful context to many of the dances that I learned aas a ballroom dancer.",
    image: `<a href="https://www.amazon.com/Century-Dance-Hundred-Musical-Movement/dp/0815411332?crid=1PC8WV1OBSB2Y&keywords=A+century+of+dance&qid=1650211117&s=books&sprefix=a+century+of+dance%2Cstripbooks%2C122&sr=1-2&linkCode=li2&tag=msc4dnc-20&linkId=d3e040773b8b72d1defb7ed1ecbffc81&language=en_US&ref_=as_li_ss_il" target="_blank"><img border="0" src="//ws-na.amazon-adsystem.com/widgets/q?_encoding=UTF8&ASIN=0815411332&Format=_SL160_&ID=AsinImage&MarketPlace=US&ServiceVersion=20070822&WS=1&tag=msc4dnc-20&language=en_US" ></a><img src="https://ir-na.amazon-adsystem.com/e/ir?t=msc4dnc-20&language=en_US&l=li2&o=1&a=0815411332" width="1" height="1" border="0" alt="" style="border:none !important; margin:0px !important;" />`,
  },
  {
    type: BookType.History,
    title: "Frankie Manning",
    subtitle: "Ambassador of Lindy Hop",
    author: "Cynthia Millman, Frankie Manning",
    paperback: "https://amzn.to/3A5eeZ9",
    hardcover: "https://amzn.to/3tYxWlq",
    audible: "https://amzn.to/3OFPCtV",
    notes:
      "This is a wonderful story of one of the greatest Lindy Hop dancers of all time.  Not only does it cover much of the early history of Lindy Hop, but it also talks about Mr. Manning's amazing return during the Lindy Hop revival after having spent decades at the U.S. Post Office.",
    image: `<a href="https://www.amazon.com/Frankie-Manning-Ambassador-Lindy-Hop/dp/1592135641?_encoding=UTF8&qid=1656199632&sr=8-1&linkCode=li2&tag=msc4dnc-20&linkId=26c9873e1e5973edd32bee0b06f1a556&language=en_US&ref_=as_li_ss_il" target="_blank"><img border="0" src="//ws-na.amazon-adsystem.com/widgets/q?_encoding=UTF8&ASIN=1592135641&Format=_SL160_&ID=AsinImage&MarketPlace=US&ServiceVersion=20070822&WS=1&tag=msc4dnc-20&language=en_US" ></a><img src="https://ir-na.amazon-adsystem.com/e/ir?t=msc4dnc-20&language=en_US&l=li2&o=1&a=1592135641" width="1" height="1" border="0" alt="" style="border:none !important; margin:0px !important;" />`,
  },
  {
    type: BookType.History,
    title: "Swingin' at the Savoy",
    subtitle: "The Memoir of a Jazz Dancer",
    author: "Norma Miller, Evette Jensen",
    paperback: "https://amzn.to/3Qrq98k",
    hardcover: "https://amzn.to/3nfbWPt",
    audible: "https://amzn.to/3nfvfbq",
    notes:
      "This is a beautiful memoir of one of the greatest Lindy Hop dancers of all time. Ms. Miller was not only one of dancers that defined the Lindy Hop, but as Lindy Hop faded for a while post World War II, she launched a career as a Jazz Dancer.",
    image: `<a href="https://www.amazon.com/Swingin-at-Savoy-Norma-Miller/dp/1566398495?_encoding=UTF8&qid=1656199984&sr=8-6&linkCode=li2&tag=msc4dnc-20&linkId=e5b0da9f979d004a9af19329b8f60808&language=en_US&ref_=as_li_ss_il" target="_blank"><img border="0" src="//ws-na.amazon-adsystem.com/widgets/q?_encoding=UTF8&ASIN=1566398495&Format=_SL160_&ID=AsinImage&MarketPlace=US&ServiceVersion=20070822&WS=1&tag=msc4dnc-20&language=en_US" ></a><img src="https://ir-na.amazon-adsystem.com/e/ir?t=msc4dnc-20&language=en_US&l=li2&o=1&a=1566398495" width="1" height="1" border="0" alt="" style="border:none !important; margin:0px !important;" />`,
  },
  {
    type: BookType.Fiction,
    title: "Stardance",
    author: "Spider and Jeanne Robinson",
    kindle: "https://amzn.to/3EH0N1j",
    hardcover: "https://amzn.to/3EIUUAY",
    notes:
      'This is an example of science fiction at its best. The authors take an idea, in this case, "what would it be like to dance in space," and explore it in a way that makes you see implications that make you do a double-take. At the same time, they build believable and relatable characters that carry you through the story and leave you wanting more.',
    image: `<a href="https://www.amazon.com/Stardance-novella-Spider-Robinson-ebook/dp/B007XG4KPU?dchild=1&keywords=stardance+spider+robinson&qid=1631903677&sr=8-2&linkCode=li2&tag=msc4dnc-20&linkId=53c3c92bc97f7c106f99fbb6c38c37ab&language=en_US&ref_=as_li_ss_il" target="_blank"><img border="0" src="//ws-na.amazon-adsystem.com/widgets/q?_encoding=UTF8&ASIN=B007XG4KPU&Format=_SL160_&ID=AsinImage&MarketPlace=US&ServiceVersion=20070822&WS=1&tag=msc4dnc-20&language=en_US" ></a><img src="https://ir-na.amazon-adsystem.com/e/ir?t=msc4dnc-20&language=en_US&l=li2&o=1&a=B007XG4KPU" width="1" height="1" border="0" alt="" style="border:none !important; margin:0px !important;" />`,
  },
  {
    type: BookType.Fiction,
    title: "Confessions of a Ballroom Diva",
    author: "Irene Radford",
    kindle: "https://amzn.to/39tedj9",
    paperback: "https://amzn.to/3lUfPID",
    notes:
      "This is another straight-up fun Urban Fantasy. In this case, one of the two main characters is a celebrity on a television show called “Dancing from the Heart,” who is a psychic vampire. The other main character is a judge on the show who also happens to be a member of a guild of vampire and demon hunters.",
    image: `<a href="https://www.amazon.com/Confessions-Ballroom-Diva-Artistic-Demons-ebook/dp/B07HVSVB2M?dchild=1&keywords=Confessions+of+a+Ballroom+Diva&qid=1631554407&s=digital-text&sr=1-1&linkCode=li2&tag=msc4dnc-20&linkId=4ab9b19e3a38b5a14687632eb2261144&language=en_US&ref_=as_li_ss_il" target="_blank"><img border="0" src="//ws-na.amazon-adsystem.com/widgets/q?_encoding=UTF8&ASIN=B07HVSVB2M&Format=_SL160_&ID=AsinImage&MarketPlace=US&ServiceVersion=20070822&WS=1&tag=msc4dnc-20&language=en_US" ></a><img src="https://ir-na.amazon-adsystem.com/e/ir?t=msc4dnc-20&language=en_US&l=li2&o=1&a=B07HVSVB2M" width="1" height="1" border="0" alt="" style="border:none !important; margin:0px !important;" />`,
  },
  {
    type: BookType.Fiction,
    title: "InCryptid",
    author: "Seanan McGuire",
    kindle: "https://amzn.to/3rwSjF9",
    others: [
      {
        text: "InCryptid 1: Discount Armageddon (paperback)",
        ref: "https://amzn.to/3JPukY0",
      },
      {
        text: "InCryptid 2: Midnight Blue-Light Special (paperback)",
        ref: "https://amzn.to/3KTb3q6",
      },
    ],
    notes:
      "The first two books in the series feature a Professional Ballroom Dancer who happens to be a “crypto-zoologist” who studies and protects fantastic creatures that live unseen among us in the modern world. Seanan, if you're reading this, please consider bring Verity back.  This series is straight-up fun urban fantasy.",
    image: `<a href="https://www.amazon.com/gp/product/B006LU0HNS?storeType=ebooks&linkCode=li2&tag=msc4dnc-20&linkId=c1a8480514018676bad081f7b882c7ec&language=en_US&ref_=as_li_ss_il" target="_blank"><img border="0" src="//ws-na.amazon-adsystem.com/widgets/q?_encoding=UTF8&ASIN=B006LU0HNS&Format=_SL160_&ID=AsinImage&MarketPlace=US&ServiceVersion=20070822&WS=1&tag=msc4dnc-20&language=en_US" ></a><img src="https://ir-na.amazon-adsystem.com/e/ir?t=msc4dnc-20&language=en_US&l=li2&o=1&a=B006LU0HNS" width="1" height="1" border="0" alt="" style="border:none !important; margin:0px !important;" />`,
  },
  // {
  //   type: BookType.Education,
  //   subtitle: "",
  //   title: "",
  //   author: "",
  //   kindle: "",
  //   paperback: "",
  //   hardcover: "",
  //   review: "",
  //   notes: "",
  //   image: ``,
  // },
];

const breadcrumbs: BreadCrumbItem[] = [...infoTrail, { text: "Reading List", active: true }];

// INT-TODO: Don't think this needs to be computed...
const categories = computed(() => {
  return [
    new BookCategory(
      BookType.Education,
      "Learning to Dance",
      "Books about learning to dance and to become a better dancer",
      books,
    ),
    new BookCategory(
      BookType.History,
      "Dance History",
      "Books about dance history and the social impact of dance",
      books,
    ),
    new BookCategory(
      BookType.Fiction,
      "Dance in Fiction",
      "Books that celebrate dance in fiction",
      books,
    ),
  ];
});

const flattened = computed(() => {
  const params = new URLSearchParams(window.location.search);
  const flat = params.get("flat");

  return !!flat && flat.toLowerCase() === "true";
});
</script>

<template>
  <PageFrame id="app" title="Reading List" :breadcrumbs="breadcrumbs">
    <div v-if="flattened">
      <div v-for="category in categories" :key="category.type">
        {{ category.title }}
        <ReadingCategory :category="category" />
      </div>
    </div>
    <BTabs v-else>
      <BTab
        v-for="category in categories"
        :key="category.type"
        :id="'bvt-' + category.type"
        :title="category.title"
      >
        <ReadingCategory :category="category" />
      </BTab>
    </BTabs>
    <div>
      <h2>About the links on this page</h2>
      <p>
        If you found a book through this
        <a href="https://www.music4dance.net">site</a> or the associated
        <a href="https://music4dance.blog">blog</a> please be kind and click on a link on this site
        to purchase it. This helps support the site. If you're feeling especially generous (or just
        like the site a lot) clicking on the Amazon links in the blog or the site and then doing
        your regular, unrelated shopping will also help support the site as a very small fraction of
        those proceeds will be directed to musci4dance.
      </p>
      <p>
        Also, if you are interested in signing up for audible to "read" any of the recommended
        books, please consider doing so through the link below. Similaraly, if you are considering
        trying Amazon music, signing up through the link below will help out music4dance.
      </p>
    </div>
    <iframe
      src="//rcm-na.amazon-adsystem.com/e/cm?o=1&p=12&l=ur1&category=audibleplus&banner=0MG2XKQ7PYPP84NBNFR2&f=ifr&lc=pf4&linkID=20e48696049a1c82b9a4f031746f4339&t=msc4dnc-20&tracking_id=msc4dnc-20"
      width="300"
      height="250"
      scrolling="no"
      border="0"
      marginwidth="0"
      style="border: none"
      frameborder="0"
      sandbox="allow-scripts allow-same-origin allow-popups allow-top-navigation-by-user-activation"
    ></iframe>
    <iframe
      src="//rcm-na.amazon-adsystem.com/e/cm?o=1&p=12&l=ur1&category=primemusic&banner=0Y451P54C03XJ9ZRPK82&f=ifr&lc=pf4&linkID=d2af141871b51c4f9ec4ce1e95a3b479&t=msc4dnc-20&tracking_id=msc4dnc-20"
      width="300"
      height="250"
      scrolling="no"
      border="0"
      marginwidth="0"
      style="border: none"
      frameborder="0"
      sandbox="allow-scripts allow-same-origin allow-popups allow-top-navigation-by-user-activation"
    ></iframe>
  </PageFrame>
</template>

<style lang="scss" scoped>
.category-description {
  margin-top: 1em;
  font-size: 1.25em;
  font-weight: lighter;
}
</style>
