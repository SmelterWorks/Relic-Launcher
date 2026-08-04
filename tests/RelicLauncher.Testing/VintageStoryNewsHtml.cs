namespace RelicLauncher.Testing;

public static class VintageStoryNewsHtml
{
    public const string SingleArticle = """
        <article class='cCmsCategoryFeaturedEntry'>
        <h2 class='ipsType_pageTitle'>
        <a href="https://www.vintagestory.at/blog.html/news/v1226-server-safety-patch-2-r448/" title="Read more">
        v1.22.6 - Server safety patch #2
        </a>
        </h2>
        <p class='ipsType_light ipsType_reset'>
        By <a href='https://www.vintagestory.at/profile/2-tyron/'>Tyron</a>, in News, Friday at 12:12 PM
        </p>
        </article>
        """;

    public const string TwoArticles = """
        <h2 class='ipsType_pageTitle'>
        <a href="https://www.vintagestory.at/blog.html/news/first-r1/">First &amp; title</a>
        </h2>
        <p class='ipsType_light ipsType_reset'>By Author A</p>
        <h2 class='ipsType_pageTitle'>
        <a href="https://www.vintagestory.at/blog.html/news/second-r2/">Second title</a>
        </h2>
        <p class='ipsType_light ipsType_reset'>By Author B</p>
        """;

    public const string DuplicateUrls = """
        <h2 class='ipsType_pageTitle'>
        <a href="https://www.vintagestory.at/blog.html/news/same-r1/">First</a>
        </h2>
        <h2 class='ipsType_pageTitle'>
        <a href="https://www.vintagestory.at/blog.html/news/same-r1/">Duplicate</a>
        </h2>
        """;

    public const string HtmlEntitiesInTitle = """
        <h2 class='ipsType_pageTitle'>
        <a href="https://www.vintagestory.at/blog.html/news/entities-r1/">Rock &amp; Stone &lt;beta&gt;</a>
        </h2>
        """;

    public const string ArticleWithJsonBody = """
        <html>
        <span class='ipsType_break ipsContained'>Patch notes</span>
        <p class='ipsType_light ipsType_reset'>By Tyron, in News</p>
        <script type="application/ld+json">
        {
            "headline": "Patch notes",
            "articleBody": "Dear players\\n\\nThis is a test update."
        }
        </script>
        </html>
        """;

    public const string ArticleWithSectionMedia = """
        <span class='ipsType_break ipsContained'>Media post</span>
        <section class="ipsType_richText ipsContained ipsType_normal">
        <p>Hello survivors</p>
        <a class="ipsAttachLink ipsAttachLink_image" href="//media.vintagestory.at/monthly_2026_07/image.png">
        <img src="//media.vintagestory.at/monthly_2026_07/image.thumb.png" alt="Screenshot" />
        </a>
        <iframe src="https://www.youtube.com/embed/dQw4w9WgXcQ"></iframe>
        </section>
        """;
}
