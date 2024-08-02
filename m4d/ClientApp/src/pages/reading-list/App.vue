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
    image: `<a href="https://amzn.to/2ZKMWRS" target="_blank"><img border="0" src="https://m.media-amazon.com/images/I/51UsyBUQX-L._SL160_.jpg" ></a>`,
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
    image: `<a href="https://amzn.to/47j07NO" target="_blank"><img border="0" src="https://m.media-amazon.com/images/I/51YTVr9kdJL._SL160_.jpg" ></a>`,
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
    image: `<a href="https://amzn.to/2SVUQKY" target="_blank"><img border="0" src="https://m.media-amazon.com/images/I/51J+O45qFxL._SL160_.jpg" ></a>`,
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
    image: `<a href="https://amzn.to/3xJDSyd" target="_blank"><img border="0" src="https://m.media-amazon.com/images/I/51J3-EhaN1L._SL160_.jpg" ></a>`,
  },
  {
    type: BookType.Education,
    title: "Dance Better Now",
    subtitle: "The secret to mastering dance and dancing at the excellent standard you want by",
    author: "Clint Steele",
    kindle: "https://amzn.to/2Lfq7Sx",
    notes: "This books is an educator’s take on how to rapidly improve your level of social dance.",
    image: `<a href="https://amzn.to/2Lfq7Sx" target="_blank"><img border="0" src="https://m.media-amazon.com/images/I/41f8JvytwnL._SL160_.jpg" ></a>`,
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
    image: `<a href="https://amzn.to/3CUlzw9" target="_blank"><img border="0" src="https://m.media-amazon.com/images/I/41CFV0F8eSL._SL160_.jpg"" ></a>`,
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
    image: `<a href="https://amzn.to/2NKZTZH" target="_blank"><img border="0" src="https://m.media-amazon.com/images/I/31YwDfSgaRL._SL160_.jpg" ></a>`,
  },
  {
    type: BookType.History,
    title: "A Century of Dance",
    subtitle: "A Hundred Years of Musical Movement, from Waltz to Hip Hop",
    author: "Ian Driver",
    paperback: "https://amzn.to/3MamSIx",
    notes:
      "This is a large format book with many amazing illustrations.  But the best part of this books from my perspective is that it gives an easy to read overview of the history of dance in the Twentieth Century, giving some useful context to many of the dances that I learned aas a ballroom dancer.",
    image: `<a href="https://amzn.to/3MamSIx" target="_blank"><img border="0" src="https://m.media-amazon.com/images/I/51sh9KwRw7L._SL160_.jpg" ></a>`,
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
    image: `<a href="https://amzn.to/3A5eeZ9" target="_blank"><img border="0" src="https://m.media-amazon.com/images/I/41bbpvA3yLL._SL160_.jpg" ></a>`,
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
    image: `<a href="https://amzn.to/3Qrq98k" target="_blank"><img border="0" src="https://m.media-amazon.com/images/I/41Ux3Qn9PKL._SL160_.jpg" ></a>`,
  },
  {
    type: BookType.Fiction,
    title: "Stardance",
    author: "Spider and Jeanne Robinson",
    kindle: "https://amzn.to/3EH0N1j",
    hardcover: "https://amzn.to/3EIUUAY",
    notes:
      'This is an example of science fiction at its best. The authors take an idea, in this case, "what would it be like to dance in space," and explore it in a way that makes you see implications that make you do a double-take. At the same time, they build believable and relatable characters that carry you through the story and leave you wanting more.',
    image: `<a href="https://amzn.to/3EH0N1j" target="_blank"><img border="0" src="https://m.media-amazon.com/images/I/51lcqMNgpYL._SL160_.jpg" ></a>`,
  },
  {
    type: BookType.Fiction,
    title: "Confessions of a Ballroom Diva",
    author: "Irene Radford",
    kindle: "https://amzn.to/39tedj9",
    paperback: "https://amzn.to/3lUfPID",
    notes:
      "This is another straight-up fun Urban Fantasy. In this case, one of the two main characters is a celebrity on a television show called “Dancing from the Heart,” who is a psychic vampire. The other main character is a judge on the show who also happens to be a member of a guild of vampire and demon hunters.",
    image: `<a href="https://amzn.to/39tedj9" target="_blank"><img border="0" src="https://m.media-amazon.com/images/I/51vCM3c9SBL._SL160_.jpg" ></a>`,
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
    image: `<a href="https://amzn.to/3rwSjF9" target="_blank"><img border="0" src="https://m.media-amazon.com/images/I/51c83ifjzjL._SL160_.jpg" ></a>`,
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
        <ReadingCategory :category="category" :hide-images="true" />
      </div>
    </div>
    <BTabs v-else>
      <BTab v-for="category in categories" :key="category.type" :title="category.title">
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
        books, please consider doing so through
        <a href="https://amzn.to/3zlOVUq" target="_blank">this link</a>. Similaraly, if you are
        considering trying Amazon music, signing up through
        <a href="https://amzn.to/45Q4yzG" target="_blank">this link</a> will help out music4dance.
        And finally, if your thinking about trying Kindle Unlimited, please consider doing so
        through <a href="https://amzn.to/4eGK1S0" target="_blank">this link</a>.
      </p>
    </div>
  </PageFrame>
</template>

<style lang="scss" scoped>
.category-description {
  margin-top: 1em;
  font-size: 1.25em;
  font-weight: lighter;
}
</style>
