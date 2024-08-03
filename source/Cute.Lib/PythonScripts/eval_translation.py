import json
import nltk

nltk.download('wordnet', quiet=True)
from nltk.translate.gleu_score import sentence_gleu
from nltk.translate.meteor_score import single_meteor_score
from hlepor import single_hlepor_score

from deepeval import evaluate
from deepeval.test_case import LLMTestCase
from deepeval.metrics import BaseMetric


class EvalTranslation:
    """
    Class for evaluating translation metrics using DeepEval library.
    """

    def __init__(self, llm_model: str, th: float, input: str, actual_output: str, expected_output: str) :
        """
        Initializes an instance of EvalTranslation.

        Args:
            llm_model (str): The language model used for evaluation.
            th (float): The threshold score for the metrics.
            input (str): The input text for translation.
            actual_output (str): The actual translated output.
            expected_output (str): The expected translated output.
        """
        self.test_case = LLMTestCase(input=input, actual_output=actual_output, expected_output=expected_output)
        self.gleu = GleuMetric(tresh_score=th, model=llm_model)
        self.meteor = MeteorMetric(tresh_score=th, model=llm_model)
        self.lepor = LeporMetric(tresh_score=th, model=llm_model)
        return self

    def evaluate(self) -> str:
        """
        Evaluates the translation metrics for the given test case.

        Returns:
            str: The evaluation results in JSON format.
        """
        test_results = evaluate(test_cases=[self.test_case], 
                                metrics=[self.gleu, self.meteor, self.lepor],
                                print_results=False)
        return str(test_results)

    def measure(self, metric: str) -> str:
        """
        Measures a specific translation metric for the given test case.

        Args:
            metric (str): The metric to measure ("gleu", "meteor", or "lepor").

        Returns:
            str: The measurement results in JSON format.
        """
        if metric == "gleu":
            self.gleu.measure(self.test_case)
            result = {
                "Result": self.gleu.success,
                "Score": self.gleu.score,
                "Reason": self.gleu.reason
            }
            return json.dumps(result)
            
        elif metric == "meteor":
            self.meteor.measure(self.test_case)
            result = {
                "Result": self.meteor.success,
                "Score": self.meteor.score,
                "Reason": self.meteor.reason
            }
            return json.dumps(result)
                        
        elif metric == "lepor":
            self.lepor.measure(self.test_case)
            result = {
                "Result": bool(self.lepor.success),
                "Score": self.lepor.score,
                "Reason": self.lepor.reason
            }
            return json.dumps(result)


###############################################################################################################
# Custom Metrics
# The following custom metrics are implemented as subclasses of the BaseMetric class from the DeepEval library.
###############################################################################################################
class MeteorMetric(BaseMetric):
    """
    The Metric for Evaluation of Translation with Explicit ORdering (METEOR) 
    (see https://www.cs.cmu.edu/~alavie/METEOR/pdf/Lavie-Agarwal-2007-METEOR.pdf) is a metric for the evaluation of MT output. 
    The metric is based on the harmonic mean of unigram precision and recall, with recall weighted higher than precision. 
    It also has several features that are not found in other metrics, such as stemming and synonymy matching, along with 
    the standard exact word matching. The metric was designed to fix some of the problems found in the more popular BLEU metric, 
    and also produce good correlation with human judgement at the sentence or segment level. This differs from the BLEU metric 
    in that BLEU seeks correlation at the corpus level. The METEOR metric ranges from 0 to 1, where 1 indicates a perfect match.
    """

    def __init__(self, tresh_score: float=0.5, model: str=None):
        """
        Initialize the MeteorMetric object.

        Args:
            tresh_score (float): The threshold score for success. Default is 0.5.
            model (str): The model used for evaluation. Default is None.
        """
        self.threshold = tresh_score
        self.model = model

    def measure(self, test_case: LLMTestCase):
        """
        Measure the METEOR score for a given test case.

        Args:
            test_case (LLMTestCase): The test case to evaluate.

        Returns:
            float: The METEOR score for the test case.
        """
        actual_output = test_case.actual_output.replace('.', '').split(' ')
        expected_output = test_case.expected_output.replace('.', '').split(' ')

        self.success = round(single_meteor_score(expected_output, actual_output), 2) >= self.threshold 
        if self.success:
            self.score = 1.0
            self.reason = f"The METEOR score is { self.score } because the generated translation is a close or exact match to the reference translation."
        else:
            self.score = 0.0
            self.reason = f"The METEOR score is { self.score } because the generated translation chose the wrong words, eaving things out, or scrambling the sentence order. Even if the words are mostly correct, if the translation sounds awkward or misses the key idea, it won't score well."
            
        return self.score

    async def a_measure(self, test_case: LLMTestCase):
        """
        Asynchronously measure the METEOR score for a given test case.

        Args:
            test_case (LLMTestCase): The test case to evaluate.

        Returns:
            float: The METEOR score for the test case.
        """
        return self.measure(test_case)
    
    def is_successful(self):
        """
        Check if the measurement was successful.

        Returns:
            bool: True if the measurement was successful, False otherwise.
        """
        return self.success

    @property
    def __name__(self):
        """
        Get the name of the metric.

        Returns:
            str: The name of the metric.
        """
        return "METEOR"
    

class GleuMetric(BaseMetric):
    """
    GleuMetric:
    The GLEU metric is a sentence-level evaluation metric that is similar to BLEU but is designed to be used for single sentences. 
    Proposed by Yonghui Wu et al (see https://arxiv.org/pdf/1609.08144v2.pdf), the GLEU metric is calculated by recording all 
    sub-sequences of 1, 2, 3, or 4 tokens in the output and target sequences (n-grams). Then, the authors compute a recall, 
    which is the ratio of the number of matching n-grams to the number of total n-grams in the target (ground truth) sequence, 
    and a precision, which is the ratio of the number of matching n-grams to the number of total n-grams in the 
    generated output sequence. The GLEU metric is simply the minimum of recall and precision. 
    This GLEU metric's range is always between 0 (no matches) and 1 (all match) and it is symmetrical when switching output and 
    target. GLEU metric correlates quite well with the BLEU metric on a corpus level but does not have its drawbacks per sentence 
    reward objective.
    """

    def __init__(self, tresh_score: float=0.5, model: str=None):
        """
        Initialize the GleuMetric object.

        Args:
            tresh_score (float): The threshold score for success. Default is 0.5.
            model (str): The model name. Default is None.
        """
        self.threshold = tresh_score
        self.model = model

    def measure(self, test_case: LLMTestCase):
        """
        Measure the GLEU score for a given test case.

        Args:
            test_case (LLMTestCase): The test case object containing the expected and actual output.

        Returns:
            float: The GLEU score for the test case.
        """
        expected_output = test_case.expected_output.replace('.', '').split(' ')
        actual_output = test_case.actual_output.replace('.', '').split(' ')

        self.success = round(sentence_gleu([expected_output], actual_output), 2) >= self.threshold 
        if self.success:
            self.score = 1.0
            self.reason = f"The GLEU score is { self.score } because the generated translation is a close or exact match to the reference translation."
        else:
            self.score = 0.0
            self.reason = f"The GLEU score is { self.score } because the are significant differences between the generated translation and the reference translation, such as incorrect word choices, poor grammar, or missing key information."

        return self.score

    async def a_measure(self, test_case: LLMTestCase):
        """
        Asynchronously measure the GLEU score for a given test case.

        Args:
            test_case (LLMTestCase): The test case object containing the expected and actual output.

        Returns:
            float: The GLEU score for the test case.
        """
        return self.measure(test_case)
    
    def is_successful(self):
        """
        Check if the GLEU metric is successful.

        Returns:
            bool: True if the metric is successful, False otherwise.
        """
        return self.success

    @property
    def __name__(self):
        """
        Get the name of the GLEU metric.

        Returns:
            str: The name of the metric.
        """
        return "GLEU"


class LeporMetric(BaseMetric):
    """
    LeporMetric: The Length Penalty, Precision, n-gram Position difference Penalty and Recall (LEPOR) metric 
    is an automatic MT metric with tunable parameters and reinforced factors. The hLEPOR score represents the harmonic mean 
    of enhanced Length Penalty, Precision, n-gram Position difference Penalty and Recall proposed by Aaron Li-Feng Han, 
    Derek F. Wong, Lidia S. Chao, Liangye He Yi Lu, Junwen Xing, and Xiaodong Zeng.
    
    The LEPOR metric ranges from 0 to 1, where 1 indicates a perfect match between the reference and hypothesis.
    """

    def __init__(self, tresh_score: float=0.5, model: str=None):
        """
        Initialize the LeporMetric object.

        Args:
            tresh_score (float): The threshold score for success. Default is 0.5.
            model (str): The model used for evaluation. Default is None.
        """
        self.threshold = tresh_score
        self.model = model

    def measure(self, test_case: LLMTestCase):
        """
        Measure the LEPOR score for a given test case.

        Args:
            test_case (LLMTestCase): The test case containing the expected and actual outputs.

        Returns:
            float: The LEPOR score, ranging from 0 to 1.
        """
        self.success = round(single_hlepor_score(
            reference=test_case.expected_output, 
            hypothesis=test_case.actual_output), 2) >= self.threshold 
        if self.success:
            self.score = 1.0
            self.reason = f"The LEPOR score is { self.score } because the generated translation is a close or exact match to the reference translation."
        else:
            self.score = 0.0
            self.reason = f"The LEPOR score is { self.score } because the generated translation has poor lexical similarity, inadequate precision, and recall, as well as significant differences in word order compared to the reference translation."

        return self.score

    async def a_measure(self, test_case: LLMTestCase):
        """
        Asynchronously measure the LEPOR score for a given test case.

        Args:
            test_case (LLMTestCase): The test case containing the expected and actual outputs.

        Returns:
            float: The LEPOR score, ranging from 0 to 1.
        """
        return self.measure(test_case)
    
    def is_successful(self):
        """
        Check if the LEPOR measurement was successful.

        Returns:
            bool: True if the measurement was successful, False otherwise.
        """
        return self.success

    @property
    def __name__(self):
        """
        Get the name of the LeporMetric.

        Returns:
            str: The name of the metric.
        """
        return "LEPOR"
