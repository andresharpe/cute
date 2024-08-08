import os
import requests
import json

from bs4 import BeautifulSoup

from deepeval.test_case import LLMTestCase
from deepeval.metrics import BaseMetric


class EvalSeo:
    def __init__(self, input: str, keyword: str, related_keywords: str, th: float):
        self.test_case = LLMTestCase(actual_output=input)
        self.content_analysis_metric = ContentAnalysisMetric(tresh_score=th, keyword=keyword, related_keywords=related_keywords)
        return self

    def measure(self):
        self.content_analysis_metric.measure(self.test_case)
        result = {
            "Result": self.content_analysis_metric.success,
            "Overall SEO Score": self.content_analysis_metric.overall_seo_score,
            "Title Tag": {
                "Score": self.content_analysis_metric.title_tag_score,
                "Feedback": self.content_analysis_metric.title_tag_feedback
            },
            "Meta Description": {
                "Score": self.content_analysis_metric.meta_description_score,
                "Feedback": self.content_analysis_metric.meta_description_feedback
            },
            "Page Headings": {
                "Score": self.content_analysis_metric.page_headings_score,
                "Feedback": self.content_analysis_metric.page_headings_feedback
            },
            "Content Length": {
                "Score": self.content_analysis_metric.content_length_score,
                "Feedback": self.content_analysis_metric.content_length_feedback
            },
            "On Page Links": {
                "Score": self.content_analysis_metric.on_page_links_score,
                "Feedback": self.content_analysis_metric.on_page_links_feedback
            },
            "Image": {
                "Score": self.content_analysis_metric.image_score,
                "Feedback": self.content_analysis_metric.image_feedback
            },
            "Keyword Usage": {
                "Score": self.content_analysis_metric.keyword_usage_score,
                "Feedback": self.content_analysis_metric.keyword_usage_feedback
            },
            "Related Keywords": {
                "Score": self.content_analysis_metric.related_keywords_score,
                "Related Keywords Found": self.content_analysis_metric.related_keywords_found,
                "Related Keywords Not Found": self.content_analysis_metric.related_keywords_not_found,
                "Feedback": self.content_analysis_metric.related_keywords_feedback
            }
        }
        return json.dumps(result)


#############################################################################################################
# Custom Metrics
# The following custom metric is implemented as subclasses of the BaseMetric class from the DeepEval library.
#############################################################################################################
class ContentAnalysisMetric(BaseMetric):
    """ContentOptmizationMetric:
    This class measures the overall SEO content score from SEO Review Tools API (see https://api.seoreviewtools.com/documentation/seo-content-analysis-api/).
    It receives the input method (either "url" or "content") and an API key as parameters to access the content
    analysis API."""

    # This metric by default checks if the latency is greater than 10 seconds
    def __init__(self, tresh_score: float, keyword: str=None, related_keywords: str=None):
        self.threshold = tresh_score
        self.keyword = keyword
        self.related_keywords = related_keywords

    def measure(self, test_case: LLMTestCase):
        # Set self.success and self.score in the "measure" method
        seo_analysis_result = self.analyze_seo(test_case.actual_output, self.keyword, self.related_keywords)

        # Parse the SEO analysis result
        self.overall_seo_score = float(seo_analysis_result['Overview']['Overall SEO Score'] / 100) # Overall SEO score
        self.success = self.overall_seo_score >= self.threshold 

        # Title Tag analysis
        self.title_tag_score = float(seo_analysis_result['Title Tag']['SEO Score'] / seo_analysis_result['Title Tag']['Max SEO score available'])
        self.title_tag_feedback = "\n".join(
            seo_analysis_result['Title Tag']['Feedback details']['Status']['text'],
            seo_analysis_result['Title Tag']['Feedback details']['Length']['text'],
            seo_analysis_result['Title Tag']['Feedback details']['Focus keyword']['text'],
            seo_analysis_result['Title Tag']['Feedback details']['Focus keywords position']['text'])

        # Meta Description analysis
        self.meta_description_score = float(seo_analysis_result['Meta description']['SEO Score'] / seo_analysis_result['Meta description']['Max SEO score available'])
        self.meta_description_feedback = "\n".join(
            seo_analysis_result['Meta description']['Feedback details']['Status']['text'],
            seo_analysis_result['Meta description']['Feedback details']['Length']['text'],
            seo_analysis_result['Meta description']['Feedback details']['Focus keyword']['text'],
            seo_analysis_result['Meta description']['Feedback details']['Focus keywords position']['text'])
        
        # Page Headings analysis
        self.page_headings_score = float(seo_analysis_result['Page headings']['SEO Score'] / seo_analysis_result['Page headings']['Max SEO score available'])
        self.page_headings_feedback = "\n".join(
            seo_analysis_result['Page headings']['Feedback details']['Status']['text'],
            seo_analysis_result['Page headings']['Feedback details']['Focus keyword']['text'])

        # Content Length analysis
        self.content_length_score = float(seo_analysis_result['Content length']['SEO Score'] / seo_analysis_result['Content length']['Max SEO score available'])
        self.content_length_feedback = seo_analysis_result['Content length']['Feedback details']['Status']['text']

        # On Page Links analysis
        self.on_page_links_score = float(seo_analysis_result['On page links']['SEO Score'] / seo_analysis_result['On page links']['Max SEO score available'])
        self.on_page_links_feedback = seo_analysis_result['On page links']['Feedback details']['Status']['text']

        # Image analysis
        self.image_score = float(seo_analysis_result['Image analysis']['SEO Score'] / seo_analysis_result['Image analysis']['Max SEO score available'])
        self.image_feedback = "\n".join(
            seo_analysis_result['Image analysis']['Feedback details']['Status']['text'],
            seo_analysis_result['Image analysis']['Feedback details']['Image name contains keyword']['text'],
            seo_analysis_result['Image analysis']['Feedback details']['Image ALT tag contains keyword']['text'])
        
        # Keyword Usage analysis
        self.keyword_usage_score = float(seo_analysis_result['Keyword usage']['SEO Score'] / seo_analysis_result['Keyword usage']['Max SEO score available'])
        self.keyword_usage_feedback = seo_analysis_result['Keyword usage']['Feedback details']['Status']['text']

        # Related Keywords analysis
        self.related_keywords_score = float(seo_analysis_result['Related keywords']['SEO Score'] / seo_analysis_result['Related keywords']['Max SEO score available'])
        self.related_keywords_feedback = seo_analysis_result['Related keywords']['Feedback details']['Status']['text']
        self.related_keywords_found = seo_analysis_result['Related keywords']['Related keywords found']
        self.related_keywords_not_found = seo_analysis_result['Related keywords']['Related keywords not found']
        
        return self

    async def a_measure(self, test_case: LLMTestCase):
        return self.measure(test_case)
    
    def is_successful(self):
        return self.success

    @property
    def __name__(self):
        return "SEO"
    

    # Get SEO score from API
    def analyze_seo(self, input: str, keyword: str, related_keywords: str):

        api_key = os.getenv('SEO_REVIEW_TOOLS_API_KEY')

        # URL input
        if input.startswith("http"):
            tool_request_url = "https://api.seoreviewtools.com/seo-content-analysis-4-0/?keyword={}&relatedkeywords={}&url={}&key={}".format(
                requests.utils.quote(keyword),
                requests.utils.quote(related_keywords),
                requests.utils.quote(input),
                api_key)
            seo_response = requests.request("GET", tool_request_url).json()
            return seo_response['data']
        # Content input
        else:
            title_tag = str(self.extract_html_content(input, 'title')).replace('<title>', '').replace('</title>', '') # Title tag
            meta_description = str(self.find_self_closing_tags(input, "meta", "description")[0].attrs['description'])   # Meta description
            body_content = self.extract_html_content(input, 'body') # Content to check
            body_content = ' '.join(body_content.split())   # Remove tabs and spaces
            data = {
                'content_input': {
                    'title_tag': title_tag,
                    'meta_description': meta_description,
                    'body_content': body_content.strip()
                }
            }
            keyword_input = keyword # Keyword to check
            related_keywords = related_keywords # Related keywords (optional)
        
            # URL generation with proper encoding
            tool_request_url = "https://api.seoreviewtools.com/v5/seo-content-optimization/?content=1&keyword={}&relatedkeywords={}&key={}".format(
                requests.utils.quote(keyword_input),
                requests.utils.quote(related_keywords),
                api_key
            )
            seo_response = requests.post(tool_request_url, json=data).json()
            return seo_response['data']
    
    # Function to extract content within a specific HTML tag
    def extract_html_content(html_content, tag):
        start_index = html_content.find(f"<{tag}")
        end_index = html_content.find(f"</{tag}>") + len(f"</{tag}>")
        extracted_content = html_content[start_index:end_index] # Extract the substring within the tag
        return extracted_content
    
    # Function to find all self-closing tags with a specific attribute
    def find_self_closing_tags(html_content, tag, attribute):
        soup = BeautifulSoup(html_content, 'html.parser')
        matching_tags = soup.find_all(tag, attrs={attribute: True})
        return matching_tags
