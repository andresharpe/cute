import requests
import json

from deepeval import evaluate
from deepeval.test_case import LLMTestCase
from deepeval.metrics import BaseMetric


class EvalSeo:
    def __init__(self):
        pass

    def run(self):
        # Test case metrics
        seo_analysis_metric = ContentAnalysisMetric(
            tresh_score=0.75, 
            model="gpt-4o-mini", 
            input_method="url", 
            api_key="<YOUR_API_KEY>"
        )

        # Test case parameters
        test_case_params = {
            "input": "Write a 200-220 word copy for a website that is SEO optimized.", 
            "actual_output": "https://www.example.com/seo-optimized-website"
        }

        # Test case
        test_case_seo = LLMTestCase(
            input = test_case_params["input"],
            actual_output = test_case_params["actual_output"]
        )

        # Evaluate test case
        evaluate([test_case_seo], [seo_analysis_metric])


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
    def __init__(self, tresh_score: float=0.75, model: str=None, input_method: str=None, api_key: str=None):
        self.threshold = tresh_score
        self.model = model
        self.input_method = input_method
        self.api_key = api_key

    def measure(self, test_case: LLMTestCase):
        # Set self.success and self.score in the "measure" method
        if self.input_method == "url":
            seo_score = self.get_seo_score(test_case.actual_output)
        else:
            seo_score = self.get_seo_score(test_case.actual_output)

        self.success = seo_score >= self.threshold 
        if self.success:
            self.score = 1
        else:
            self.score = 0

        self.reason = "Feedback from API"

        return self.score

    async def a_measure(self, test_case: LLMTestCase):
        return self.measure(test_case)
    
    def is_successful(self):
        return self.success

    @property
    def __name__(self):
        return "BLEU"
    
    def curl_function(tool_request_url, data):
        response = requests.post(tool_request_url, json=data)
        return response.json()
    
    # Get SEO score from content
    def get_seo_score(self, content: str):
        # Title tag
        title_tag = 'Example Copywriting Guide - Expert Tips'
        # Meta description
        meta_description = 'Master the art of copywriting with expert tips and techniques. Create compelling content and drive results with our guide'
        # Content to check
        body_content = content
        # Remove tabs and spaces
        body_content = ' '.join(body_content.split())

        data = {
            'content_input': {
                'title_tag': title_tag,
                'meta_description': meta_description,
                'body_content': body_content.strip()
            }
        }

        # Keyword to check
        keyword_input = "Copywriting Guide"
        # Related keywords (optional)
        related_keywords = "Search engine optimization|Headlines|SEO content audit|Content analysis methods|Content analysis research|SEO-friendly content"
        # URL generation with proper encoding
        tool_request_url = "https://api.seoreviewtools.com/v5/seo-content-optimization/?content=1&keyword={}&relatedkeywords={}&key={}".format(
            requests.utils.quote(keyword_input),
            requests.utils.quote(related_keywords),
            self.api_key
        )

        seo_data_json = self.curl_function(tool_request_url, data)
        return seo_data_json['data']['Overview']['Overall SEO score']
        
    # Get SEO score from URL
    def get_seo_score(self, url: str):
        # keyword
        keyword = 'Content marketing'
        # related keywords (optional)
        relatedKeywords = 'Content creation|marketing strategy|brand awareness|content marketing strategy|high quality content|target audience|online pr|sharing content|content marketing definition|content marketing examples|content distribution|promoting content'
        # input URL
        inputUrl = url
        # API URL
        toolRequestUrl = 'https://api.seoreviewtools.com/seo-content-analysis-4-0/?keyword='+keyword+'&relatedkeywords='+relatedKeywords+'&url='+inputUrl+'&key='+self.api_key

        r = requests.request("GET", toolRequestUrl,)
        r.text['data']['Overview']['Overall SEO score']
