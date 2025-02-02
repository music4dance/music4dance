<script setup lang="ts">
import { type BreadCrumbItem, infoTrail } from "@/models/BreadCrumbItem";
import BlogSeries from "./BlogSeries.vue";
import BlogArticle from "./BlogArticle.vue";

const breadcrumbs: BreadCrumbItem[] = [...infoTrail, { text: "Technical Blog", active: true }];

// TODONEXT: Fill out series...
</script>

<template>
  <PageFrame id="app" title="Technical Blog" :breadcrumbs="breadcrumbs">
    <p>
      I've started a <a href="https://medium.com/@dwgray">Medium blog</a> to chronicle some of the
      more interesting (in my opinion) technical challenges I encounter, many of which resulted from
      issues while building the music4dance website. While I have been happy with Medium overall,
      they don't do a great job of making it easy to organize my articles, so I will devote a page
      of this site to doing just that. A little more detail on my thinking about how the technical
      blog and music4dance relate is available in my
      <a href="https://music4dance.blog/2023/09/11/introducing-the-music4dance-technical-blog/"
        >post on that subject</a
      >
      in the music4dance blog.
    </p>

    <BlogSeries title="Building a multi-page website using Asp.Net Core and Vue.js">
      <template #summary>
        This is a loose series of articles that relate specifically to problems I've encountered
        using asp.net core, vue.js and vite.js to build the music4dance website. This includes a
        core series about how I got this setup working when moving to Bue.js 3 and Vite.js and I
        will be adding other stand-along articles that apply to this general topic.
      </template>
      <BlogArticle
        title="Removing Boilerplate Code from a Vite/Vue.js Multipage Website"
        link="https://medium.com/@dwgray/removing-boilerplate-code-from-a-vite-vue-js-multipage-website-1e6c01910ce9"
        summary="I have an allergy to duplicate code that significantly predates Hunt and Thomas’s formalization of the DRY principle. This includes…"
      />
      <BlogArticle
        title="How to Build a True Multi-Page Website Using ASP.NET, VUE.js, Vite.js"
        link="https://medium.com/@dwgray/how-to-build-a-true-multi-page-website-using-asp-net-vue-js-vite-js-ca7234b9b13b"
        summary="I’ll walk through setting up the ASP.NET core MVC website, adding the Vue.js/Vite.js client, and getting..."
      />
      <BlogArticle
        title="How to Build a True Multi-Page Website Using ASP.NET, VUE.js, Vite.js: Getting ASP.NET MVC and Vite.js to play nicely together"
        link="https://medium.com/@dwgray/how-to-build-a-true-multi-page-website-using-asp-net-da8f2d912f12"
        summary="I’ll walk through setting up the ASP.NET core MVC website, adding the Vue.js/Vite.js client, and getting..."
      />
      <BlogArticle
        title="How to Build a True Multi-Page Website Using ASP.NET, VUE.js, Vite.js: Making the Site Multi-Page with minimal copy-pasting"
        link="https://medium.com/@dwgray/how-to-build-a-true-multi-page-website-using-asp-net-7a1e9228b1a9"
        summary="I’ll finish building by demo website by walking through how I minimize the boilerplate code in the ASP.NET part..."
      />
    </BlogSeries>

    <BlogSeries title="The Write Once Run Anywhere (WORA) series">
      <template #summary>
        My first series of articles was inspired when I led a group of students contributing to the
        <a href="https://flutter.dev/">Google Flutter</a> project. It's an interleaved set of
        articles alternating between technical articles on writing a multi-platform app (a
        pared-down version of the <a href="/home/counter">Tempo Counter</a> app available on this
        site) and some of my personal history contributing to multi-platform tools.
      </template>
      <BlogArticle
        title="Which Multi-platform framework should I use to write my app?"
        link="https://medium.com/@dwgray/which-multi-platform-framework-should-i-use-to-write-my-app-f1afbf330493"
      >
        Or: <a href="https://learn.microsoft.com/en-us/dotnet/maui/what-is-maui">.NET MAUI</a>,
        <a href="https://flutter.dev/">Flutter</a>,
        <a href="https://reactnative.dev/">React Native</a>,
        <a href="https://ionicframework.com/">Ionic Framework</a>,
        <a href="https://www.codenameone.com/">CodeNameOne</a>,
        <a href="https://nativescript.org/">NativeScript</a>,
        <a href="https://kotlinlang.org/lp/multiplatform/">Kotlin Multiplatform</a>,
        <a href="https://unity.com/">Unity</a>, oh my…
      </BlogArticle>
      <BlogArticle
        title="Which Multi-platform framework should I use to write my app? Let’s try Flutter."
        link="https://medium.com/@dwgray/which-multi-platform-framework-should-i-use-to-write-my-app-lets-try-flutter-2c39dedd3cd3"
        summary="The one where I do my best to quickly cobble together my sample app using Google’s Flutter and share my first impressions."
      />
      <BlogArticle
        title="A personal history of Write Once Run Anywhere (WORA) — Part I, the ‘80s."
        link="https://medium.com/@dwgray/a-personal-history-of-write-once-run-anywhere-wora-part-i-the-80s-299032de6e78"
        summary="The one about BASIC, UCSD Pascal, MacFORTH, Cross-Compilation, CISC and more."
      />
      <BlogArticle
        title="Which Multi-platform framework should I use to write my app? Let’s try .NET MAUI."
        link="https://medium.com/@dwgray/which-multi-platform-framework-should-i-use-to-write-my-app-lets-try-net-maui-d5f22126f9a6"
        summary="The one where I do my best to quickly cobble together my sample app using Microsoft’s .NET MAUI and share my first impressions."
      />
      <BlogArticle
        title="A Personal History of WORA — Part II: Debuggers and Visual C++"
        link="https://medium.com/@dwgray/a-personal-history-of-wora-part-ii-debuggers-and-visual-c-47546acf4cb4"
        summary="The one where I help to debug “everywere”."
      />
      <BlogArticle
        title="Which Multi-platform framework should I use to write my app? Let’s try React Native."
        link="https://medium.com/@dwgray/which-multi-platform-framework-should-i-use-to-write-my-app-lets-try-react-native-8c563fc7f1dd"
        summary="The one where I do my best to quickly cobble together my sample app using React Native and share my first impressions."
      />
      <BlogArticle
        title="Which Multi-platform framework should I use to write my app: Conclusions from the first round"
        link="https://medium.com/@dwgray/which-multi-platform-framework-should-i-use-to-write-my-app-conclusions-from-the-first-round-71d7b463f0dd"
        summary="The one where I attempt to draw some conclusions from my little adventure."
      />
    </BlogSeries>

    <BlogSeries title="Miscellaneous Articles">
      <template #summary>
        These are some articles inspired by questions that have come up when mentoring students
        studing software engineering and a grab bag of tools and techniques that don't fall into the
        other series.
      </template>
      <BlogArticle
        title="A Very Simple Website: Back to the Basics"
        link="https://medium.com/@dwgray/a-very-simple-website-back-to-the-basics-1dffdc43d19b"
        summary="One of the students I’m mentoring is taking his first web development class. They ask the students to build a…"
      />
      <BlogArticle
        title="Debugging Tips: How to tell if your asynchronous code is actually asynchronous"
        link="https://medium.com/@dwgray/debugging-tips-how-to-tell-if-your-asynchronous-code-is-actually-asynchronous-5a0a80d52c8d"
        summary="I wrote some database access code that shouldn’t block the rendering of my web page, but does it? How do I tell..."
      />
      <BlogArticle
        title="Maintaining Scaffolded Code: A Case Study with Asp.Net Core Identity UI"
        link="https://medium.com/@dwgray/maintaining-scaffolded-code-case-study-with-asp-net-core-identity-ui-1ce8b6af5ebb"
        summary="My preferred way to upgrade scaffolded code between major versions, using Asp.Net Core Identity UI as a concrete example."
      />
      <BlogArticle
        title="How to do a “find and replace” beyond regular expressions in Visual Studio Code"
        link="https://medium.com/@dwgray/how-to-do-a-find-and-replace-beyond-regular-expressions-in-visual-studio-code-14e9e411165c"
        summary="Have you ever run into a situation where you need to convert a block of markup in a way that is beyond the reach of a regular expression? I…"
      />
    </BlogSeries>
  </PageFrame>
</template>
