﻿//
// JobMethodGroup.cs
//
// Author:
//       Jarl Erik Schmidt <github@jarlerik.com>
//
// Copyright (c) 2013 Jarl Erik Schmidt
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

namespace Winterday.External.Gengo.MethodGroups
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using Newtonsoft.Json.Linq;

    using Winterday.External.Gengo.Payloads;
    using Winterday.External.Gengo.Properties;

    /// <summary>
    /// Provides access to methods in the
    /// <a href="http://developers.gengo.com/v2/job/">Job</a>
    /// method group.
    /// </summary>
    public class JobMethodGroup
    {
        internal const string UriPartJob = "translate/job/";

        internal const string UriPartComment = "translate/job/{0}/comment";
        internal const string UriPartComments = "translate/job/{0}/comments";

        internal const string UriPartFeedback = "translate/job/{0}/feedback";

        internal const string UriPartPreview = "translate/job/{0}/preview";

        internal const string UriPartRevision = "translate/job/{0}/revision/{1}";
        internal const string UriPartRevisions = "translate/job/{0}/revisions";

        readonly IGengoClient _client;

        internal JobMethodGroup(IGengoClient client)
        {
            if (client == null)
                throw new ArgumentNullException("client");

            _client = client;
        }

        /// <summary>
        /// Approves a job currently in the 'Reviewable' state
        /// </summary>
        /// <param name="jobId">The job ID</param>
        /// <param name="stars">Quality rating of translation work</param>
        /// <param name="commentForTranslator">
        /// Comments for translator (optional)
        /// </param>
        /// <param name="commentForGengo">
        /// Comments for Gengo (optional)
        /// </param>
        /// <param name="gengoCommentIsPublic">
        /// Wether Gengo may publish the comment on their public website
        /// </param>
        /// <returns>Task yielding no value</returns>
        public async Task Approve(int jobId, Stars stars,
            string commentForTranslator,
            string commentForGengo,
            bool gengoCommentIsPublic)
        {
            var uri = UriPartJob + jobId;

            var data = new JObject();
            data["action"] = "approve";
            data["rating"] = ((int)stars).ToString();

            if (!String.IsNullOrWhiteSpace(commentForTranslator))
                data["for_translator"] = commentForTranslator;

            if (!String.IsNullOrWhiteSpace(commentForGengo))
            {
                data["for_mygengo"] = commentForGengo;
                data["public"] = Convert.ToInt32(gengoCommentIsPublic).ToString();
            }

            await _client.PutJsonAsync<JObject>(uri, data);
        }

        /// <summary>
        /// Gets information about a submitted job
        /// </summary>
        /// <param name="jobId">The job ID</param>
        /// <param name="includeMachineTranslation">
        /// If a machine translated preview should be provided if a human-
        /// made one is not yet avaible.
        /// </param>
        /// <returns>Task yielding job data</returns>
        public async Task<SubmittedJob> Get(int jobId,
            bool includeMachineTranslation)
        {
            var uri = UriPartJob + jobId;

            var data = new Dictionary<string, string>();
            data["pre_mt"] =
                Convert.ToInt32(includeMachineTranslation).ToString();

            var obj = await _client.GetJsonAsync<JObject>(uri, data, true);

            return new SubmittedJob(obj["job"] as JObject);
        }

        public async Task<SubmittedFileJob> GetFile(int jobId,
            bool includeMachineTranslation)
        {
            var uri = UriPartJob + jobId;

            var data = new Dictionary<string, string>();
            data["pre_mt"] =
                Convert.ToInt32(includeMachineTranslation).ToString();

            var obj = await _client.GetJsonAsync<JObject>(uri, data, true);

            return new SubmittedFileJob(obj["job"] as JObject);
        }

        /// <summary>
        /// Gets the submitted feedback for a job in the 'Approved' state
        /// </summary>
        /// <param name="jobId">The job ID</param>
        /// <returns>Task yielding comment and rating</returns>
        public async Task<Feedback> GetFeedback(int jobId)
        {
            var uri = string.Format(UriPartFeedback, jobId);

            var obj = await _client.GetJsonAsync<JObject>(uri, true);

            return new Feedback(obj["feedback"] as JObject);
        }

        /// <summary>
        /// Gets a raw JPEG with a preview of the translated work
        /// </summary>
        /// <param name="jobId">The job ID</param>
        /// <returns>Task yielding image as byte array</returns>
        public Task<byte[]> GetPreviewImage(int jobId)
        {
            var uri = string.Format(UriPartPreview, jobId);

            return _client.GetByteArrayAsync(uri, true);
        }

        /// <summary>
        /// Gets a specific revision for a given job
        /// </summary>
        /// <param name="jobId">The job ID</param>
        /// <param name="revisionId">The revision ID</param>
        /// <returns>Task yielding revision data</returns>
        public async Task<Revision> GetRevision(int jobId, int revisionId)
        {
            var uri = string.Format(UriPartRevision, jobId, revisionId);

            var rev = await _client.GetJsonPropertyAsync<JObject>("revision", uri, true);

            return new Revision(rev);
        }

        /// <summary>
        /// Gets a list of revision available for a job
        /// </summary>
        /// <param name="jobId">The job ID</param>
        /// <returns>Task yielding array of revision ids</returns>
        public async Task<TimestampedId[]> GetRevisions(int jobId)
        {
            var uri = string.Format(UriPartRevisions, jobId);

            var revs = await _client.GetJsonPropertyAsync<JArray>("revisions", uri, true);

            return revs.Values<JObject>().Select(
                o => new TimestampedId(o, "rev_id", "ctime")).ToArray();
        }
        
        /// <summary>
        /// Rejects a job in the 'Reviewable' state
        /// </summary>
        /// <param name="jobId">The job ID</param>
        /// <param name="reason">Reason for rejection (mandatory)</param>
        /// <param name="comment">Elaboration on reason (mandatory)</param>
        /// <param name="captcha">
        /// Value of captcha image. The URL for this can be obtained through
        /// the <see cref="Get"/> method.
        /// </param>
        /// <param name="requeueJob"></param>
        /// <returns></returns>
        public async Task Reject(int jobId, RejectionReason reason,
            string comment, string captcha, bool requeueJob)
        {
            if (String.IsNullOrWhiteSpace(comment))
                throw new ArgumentException(
                    Resources.RejectionCommentMandatory,
                    "comment");

            if (String.IsNullOrWhiteSpace(captcha))
                throw new ArgumentException(
                    Resources.RejectionCaptchaNotSpecified,
                    "captcha");

            var uri = UriPartJob + jobId;
            
            var data = new JObject();

            data["action"] = "reject";

            data["reason"] = reason.ToReasonString();
            data["comment"] = comment;
            data["captcha"] = captcha;
            data["follow_up"] = requeueJob ? "requeue" : "cancel";

            await _client.PutJsonAsync<JObject>(uri, data);
        }

        /// <summary>
        /// Returns a job in the 'Reviewable' state for a new revision
        /// </summary>
        /// <param name="jobId">The job ID</param>
        /// <param name="comment">Reason for new revision (mandatory)</param>
        /// <returns>Task yielding no value</returns>
        public async Task ReturnForRevision(int jobId, string comment)
        {
            var uri = UriPartJob + jobId;

            if (string.IsNullOrWhiteSpace(comment))
                throw new ArgumentException(
                    Resources.RevisionCommentMandatory);

            var data = new JObject();
            data["action"] = "revise";
            data["comment"] = comment;

            await _client.PutJsonAsync<JObject>(uri, data);
        }

        /// <summary>
        /// Cancels a job in the 'Available' state
        /// </summary>
        /// <param name="jobId">The job ID</param>
        /// <returns>Task yielding no value</returns>
        public async Task Delete(int jobId)
        {
            var uri = UriPartJob + jobId;
            
            await _client.DeleteAsync<JObject>(uri);
        }

        /// <summary>
        /// Gets a list of comments for a given job
        /// </summary>
        /// <param name="jobID">The job ID</param>
        /// <returns>Task yielding array of comments</returns>
        public async Task<Comment[]> GetComments(int jobID)
        {
            var uri = String.Format(UriPartComments, jobID);
            var thread = await _client.GetJsonPropertyAsync<JArray>("thread", uri, true);

            return thread.SelectFromObjects(
                o => new Comment(o)).ToArray();
        }

        /// <summary>
        /// Posts a new comment
        /// </summary>
        /// <param name="jobID">The job ID</param>
        /// <param name="body">Comment body (mandatory)</param>
        /// <returns>Task yielding no value</returns>
        public async Task PostComment(int jobID, string body)
        {
            if (String.IsNullOrWhiteSpace(body)) throw new ArgumentException("Comment body not provided", "body");

            var json = new JObject(new JProperty("body", body));

            await _client.PostJsonAsync<JToken>(string.Format(UriPartComment, jobID), json);
        }

    }
}
