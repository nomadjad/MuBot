using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace MuLibrary.Services
{
    public class LibraryService : ScrapingService
    {
        private readonly Regex OBJECT_IDS_REGEX = new Regex(@"<tr>\s{26}<td>[0-9]*</td>\s{26}<td>(\s{30}<a href=""/[a-z]*/|<maple-item item-id="")(?<id>[0-9]{7})("">\s{34}|"" item-name=""[^\n]*)<img src=""/images/(\w{3,4}/[0-9]{7}|error/icon)\.png"" alt=""[^\n]*""");   
        private readonly Regex TOTAL_PAGE_NUMBER_REGEX = new Regex(@"<li class=""page-item disabled"" aria-disabled=""true""><span class=""page-link"">\.\.\.</span></li>\s{158}<li class=""page-item""><a class=""page-link"" href=""https://lib\.mapleunity\.com/[a-zA-Z]*\?page=[0-9]*"">[0-9]*</a></li>\s{81}<li class=""page-item""><a class=""page-link"" href=""https://lib\.mapleunity\.com/[a-zA-Z]*\?page=[0-9]*"">(?<totalPageNumber>[0-9]*)</a></li>");

        public LibraryService(IServiceProvider provider) : base(provider) { }

        public async Task<int> GetTotalPageNumberAsync(string url)
        {
            string page = await DownloadPageAsync(url);

            try
            {
                var match = GetMatchInPage(TOTAL_PAGE_NUMBER_REGEX, page);
                var totalPageNumber = int.Parse(match.Groups["totalPageNumber"].Value);

                return totalPageNumber;
            }
            catch
            {
                _log.Log($"Invalid URL supplied or pattern is invalid trying to find Total Page Number at {url}");
                throw new ArgumentException();
            }
        }

        public async IAsyncEnumerable<string> GetObjectIDsFromUrlAsync(string url)
        {
            string page = await DownloadPageAsync(url);
            await foreach (Match match in GetMatchesInPageAsync(OBJECT_IDS_REGEX, page))
            {
                var id = match.Groups["id"].Value;
                _log.Log($"Match found at {url}: {id}");

                yield return id;
            }
        }

        public async Task<List<T>> GetObjects<T>(string objLibraryUrl, string objSearchUrl, Func<string, Task<T>> GetObjectFromId) where T : ILibraryObject
        {
            var list = new List<T>();
            var totalPageNumber = await GetTotalPageNumberAsync(objLibraryUrl);
            _log.Log($"Total page numbers: {totalPageNumber}");

            var allTasks = new List<Task>();
            using (var slim = new SemaphoreSlim(10, 20))
            {
                foreach (var index in Enumerable.Range(1, totalPageNumber))
                {
                    await slim.WaitAsync();
                    allTasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            string searchUrl = objSearchUrl + index;
                            await foreach (string id in GetObjectIDsFromUrlAsync(searchUrl))
                            {
                                try
                                {
                                    T obj = await GetObjectFromId(id);
                                    list.Add(obj);
                                    _log.Log($"{obj.Name} downloaded");
                                }
                                catch (ArgumentException ex)
                                {
                                    _log.Log($"{ex.GetType().ToString()} Error occurred loading { objLibraryUrl + id }");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _log.Log($"{ex.GetType().ToString()} occurred at index {index}");
                        }
                        finally
                        {
                            slim.Release();
                        }
                    }));
                }
                await Task.WhenAll(allTasks).ConfigureAwait(false);
            }

            _log.Log($"Download completed");
            return list;
        }
    }
}