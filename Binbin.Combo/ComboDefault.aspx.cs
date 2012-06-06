using System;
using System.Configuration;
using System.IO;
using System.Net;
using System.Text;
using Microsoft.Practices.EnterpriseLibrary.Caching;
using Microsoft.Practices.EnterpriseLibrary.Caching.Expirations;

namespace Binbin.Combo
{
    /// <summary>
    /// 参数 :  p : 要请求的文件路径 
    /// 多个地址以 ","号分割
    /// </summary>
    public partial class ComboDefault : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(this.Request.QueryString["p"]))
            {
                // 参数为空 不处理
                return;
            }

            DateTime ifModifiedSince;
            if (DateTime.TryParse(this.Request.Headers.Get("If-Modified-Since"), out ifModifiedSince))
            {
                if ((DateTime.Now - ifModifiedSince.AddHours(8)).Minutes < 30)
                {
                    this.Response.Status = "304 Not Modified";
                    this.Response.StatusCode = 304;
                    this.Response.End();
                }
            }

            string setting = ConfigurationManager.AppSettings["RemotePath"];
            string fatherPath;
            string[] paths;
            if (!string.IsNullOrEmpty(setting))
            {
                fatherPath = setting;
                paths = this.Request.QueryString["p"].Split(',');
            }
            else
            {
                paths = this.Request.QueryString["p"].Replace("/", @"\").Split(',');
                fatherPath = this.Server.MapPath("./");
            }
            this.Response.ClearHeaders();

            this.Response.ContentEncoding = Encoding.UTF8;
            this.Response.Charset = "utf-8";
            this.Response.AddHeader("Content-Type", this.GetMimeType(Path.GetExtension(fatherPath + paths[0])));
            this.Response.AddHeader("Accept-Charset", "utf-8,gb2312;");
            this.Response.AddHeader("Last-Modified", DateTime.Now.ToString("U", System.Globalization.DateTimeFormatInfo.InvariantInfo));


            //foreach (var path in paths.Where(t => File.Exists(fatherPath + t)))
            foreach (var path in paths)
            {
                this.Response.Write(this.GetBodyInCache("CacheKey_" + path, fatherPath + path));
                this.Response.Write(Environment.NewLine);
            }

            this.Response.End();
        }


        /// <summary>
        /// 从缓存中得到信息数据 
        /// </summary>
        /// <param name="cacheKey">缓存主键</param>
        /// <param name="filePath">文件路径</param>
        /// <returns></returns>
        private string GetBodyInCache(string cacheKey, string filePath)
        {
            var cache = CacheFactory.GetCacheManager();
            string info = (string)cache.GetData(cacheKey);
             if (string.IsNullOrEmpty(info))
            {
                info = this.getBodyInFile(filePath);
                this.addBodyInCache(cacheKey, info, filePath);
            }
            return info;
        }

        /// <summary>
        /// 增加有信息到缓存中
        /// </summary>
        /// <param name="cacheKey">用于引用该项的缓存键</param>
        /// <param name="value">要插入缓存中的对象</param>
        /// <param name="filePath">依赖文件路径</param>
        private void addBodyInCache(string cacheKey, string value, string filePath)
        {
            //BaseCache cache = BaseCache.Instance();
            //cache.Insert(cacheKey, value, Int32.MaxValue, filePath);
            var cache = CacheFactory.GetCacheManager();
            cache.Add(cacheKey, value, CacheItemPriority.Normal, null, new SlidingTime(TimeSpan.FromMinutes(5)));
        }

        /// <summary>
        /// 读取指定文件内容
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns></returns>
        private string getBodyInFile(string filePath)
        {
            string body = string.Empty;
            if (filePath.StartsWith("http://"))
            {
                //from http
                var request = (HttpWebRequest)WebRequest.Create(filePath);
                using (Stream requestStream = request.GetResponse().GetResponseStream())
                {
                    using (var bufferedStream = new BufferedStream(requestStream))
                    {
                        using (var r = new StreamReader(bufferedStream))
                        {
                            body = r.ReadToEnd();
                        }
                    }
                }
            }
            else
            {
                using (StreamReader fileReader = new StreamReader(filePath))
                {
                    body = fileReader.ReadToEnd();
                }
            }
            return body;
        }

        /// <summary>
        /// 根据文件后缀来获取MIME类型字符串
        /// </summary>
        /// <param name="extension">文件后缀</param>
        /// <returns></returns>
        private string GetMimeType(string extension)
        {
            string mime = string.Empty;
            extension = extension.ToLower().TrimStart('.');
            switch (extension)
            {
                case "chm":
                case "hlp": mime = "application/mshelp"; break;
                case "avi": mime = "video/x-msvideo"; break;
                case "csv": mime = "text/comma-separated-values"; break;
                case "html":
                case "htm":
                case "shtml": mime = "text/html"; break;
                case "css": mime = "text/css"; break;
                case "js": mime = "text/javascript"; break;
                case "doc":
                case "dot":
                case "docx": mime = "application/msword"; break;
                case "xla":
                case "xls":
                case "xlsx": mime = "application/msexcel"; break;
                case "ppt":
                case "pptx": mime = "application/mspowerpoint"; break;
                case "gz": mime = "application/gzip"; break;
                case "gif": mime = "image/gif"; break;
                case "bmp": mime = "image/bmp"; break;
                case "jpeg":
                case "jpg":
                case "jpe":
                case "png": mime = "image/jpeg"; break;
                case "mpeg":
                case "mpg":
                case "mpe":
                case "wmv": mime = "video/mpeg"; break;
                case "mp3":
                case "wma": mime = "audio/mpeg"; break;
                case "pdf": mime = "application/pdf"; break;
                case "txt": mime = "text/plain"; break;
                case "7z":
                case "z": mime = "application/x-compress"; break;
                case "zip": mime = "application/x-zip-compressed"; break;
                case "swf": mime = "application/x-shockwave-flash"; break;
                case "rm":
                case "rmvb": mime = "video/vnd.rn-realvideo"; break;
                default:
                    mime = "application/octet-stream";
                    break;
            }
            return mime;
        }

        /// <summary>
        /// 处理文件名头
        /// </summary>
        /// <param name="fileName"></param>
        private void processFileNameHeader(string fileName)
        {
            string userAgent = string.Empty;
            if (this.Request.UserAgent != null)
            {
                userAgent = this.Request.UserAgent.ToLower();
            }
            if (userAgent.IndexOf("msie") > -1)
            {
                fileName = ToHexString(fileName);
            }

            if (userAgent.IndexOf("firefox") > -1)
            {
                this.Response.AddHeader("Content-Disposition", "attachment;filename=\"" + fileName + "\"");
            }
            else
                this.Response.AddHeader("Content-Disposition", "attachment;filename=" + fileName);

        }
        /// <summary>
        /// 将字符串进行 16 位编码
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static string ToHexString(string s)
        {
            char[] chars = s.ToCharArray();
            StringBuilder builder = new StringBuilder();
            foreach (char t in chars)
            {
                bool needToEncode = NeedToEncode(t);
                if (needToEncode)
                {
                    string encodedString = ToHexString(t);
                    builder.Append(encodedString);
                }
                else
                {
                    builder.Append(t);
                }
            }

            return builder.ToString();
        }

        /// <summary>
        /// 判断指定字符串是否需要编码
        /// </summary>
        /// <param name="chr"></param>
        /// <returns></returns>
        private static bool NeedToEncode(char chr)
        {
            string reservedChars = "$-_.+!*'(),@=&";

            if (chr > 127)
                return true;
            if (char.IsLetterOrDigit(chr) || reservedChars.IndexOf(chr) >= 0)
                return false;

            return true;
        }

        /// <summary>
        /// 将非 Ascii 字符编码为 16 位
        /// </summary>
        /// <param name="chr"></param>
        /// <returns></returns>
        private static string ToHexString(char chr)
        {
            UTF8Encoding utf8 = new UTF8Encoding();
            byte[] encodedBytes = utf8.GetBytes(chr.ToString());
            StringBuilder builder = new StringBuilder();
            foreach (byte t in encodedBytes)
            {
                builder.AppendFormat("%{0}", Convert.ToString(t, 16));
            }

            return builder.ToString();
        }

    }
}