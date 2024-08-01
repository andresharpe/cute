import json
from deepeval.metrics import ContextualPrecisionMetric, ContextualRecallMetric, ContextualRelevancyMetric, AnswerRelevancyMetric, FaithfulnessMetric
from deepeval.test_case import LLMTestCase
from deepeval import evaluate


class EvalRAG :

    def __init__(self, llm_model: str, th: float, input: str, actual_output: str, expected_output: str, retrieval_context: str) :
        retrieval_context = retrieval_context.split("; ")
        self.test_case = LLMTestCase(input=input, actual_output=actual_output, expected_output=expected_output, retrieval_context=retrieval_context)
        self.contextual_precision = ContextualPrecisionMetric(threshold=th, model=llm_model, include_reason=True)
        self.contextual_recall = ContextualRecallMetric(threshold=th, model=llm_model, include_reason=True)
        self.contextual_relevancy = ContextualRelevancyMetric(threshold=th, model=llm_model, include_reason=True)
        self.answer_relevancy = AnswerRelevancyMetric(threshold=th, model=llm_model, include_reason=True)
        self.faithfulness = FaithfulnessMetric(threshold=th, model=llm_model, include_reason=True)
        return self

    def evaluate(self) :
        # Evaluate (--generation all : evaluates all generation metrics)
        test_results = evaluate(test_cases = [self.test_case], 
                                metrics = [
                                    self.contextual_precision, 
                                    self.contextual_recall,
                                    self.contextual_relevancy, 
                                    self.answer_relevancy, 
                                    self.faithfulness
                                ],
                                print_results=False)
        return str(test_results)

    def measure(self, metric: str) :
        match metric:
            case "precision":
                # Measure (--generation precision : evaluates only contextual precision metric)
                self.contextual_precision.measure(self.test_case)
                result = {
                    "Result": self.contextual_precision.success,
                    "Score": self.contextual_precision.score,
                    "Reason": self.contextual_precision.reason
                }
                return f"{json.dumps(result)}"

            case "recall":
                # Measure (--generation recall : evaluates only contextual recall metric)
                self.contextual_recall.measure(self.test_case)
                result = {
                    "Result": self.contextual_recall.success,
                    "Score": self.contextual_recall.score,
                    "Reason": self.contextual_recall.reason
                }
                return f"{json.dumps(result)}"
            
            case "relevancy":
                # Measure (--generation relevancy : evaluates only contextual relevancy metric)
                self.contextual_relevancy.measure(self.test_case)
                result = {
                    "Result": self.contextual_relevancy.success,
                    "Score": self.contextual_relevancy.score,
                    "Reason": self.contextual_relevancy.reason
                }
                return f"{json.dumps(result)}"
                        
            case "answer":
                # Measure (--generation answer : evaluates only answer relevancy metric)
                self.answer_relevancy.measure(self.test_case)
                result = {
                    "Result": self.answer_relevancy.success,
                    "Score": self.answer_relevancy.score,
                    "Reason": self.answer_relevancy.reason
                }
                return f"{json.dumps(result)}"

            case "faithfulness":
                # Measure (--generation faithfulness : evaluates only faithfulness metric)
                self.faithfulness.measure(self.test_case)
                result = {
                    "Result": self.faithfulness.success,
                    "Score": self.faithfulness.score,
                    "Reason": self.faithfulness.reason
                }
                return f"{json.dumps(result)}"
