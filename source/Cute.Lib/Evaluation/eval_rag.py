from deepeval.metrics import ContextualPrecisionMetric, ContextualRecallMetric, ContextualRelevancyMetric, AnswerRelevancyMetric, FaithfulnessMetric
from deepeval.test_case import LLMTestCase
from deepeval import evaluate


class EvalRAG :
    input = """Write a 200-210 word website intro as to why Canton of Zug in Switzerland is good for setting up your business 
                        and having an office there. Mention some of the famous businesses that are headquartered there, 
                        industries based there, major transport hubs in the vicinity and other facts relevant to doing business there.

                        Make sure you include the phrase '''Office space in Canton of Zug''' in the intro.

                        Output your response in bullets using valid Markdown syntax. Use '''-''' for a list prefix 
                        but don't make use of bold, underline or other Markdown."""
    actual_output = """    - Nestled in the heart of Switzerland, Canton of Zug is a prime spot for creative entrepreneurs looking to establish their next venture. Its strategic location offers quick access to major cities such as Zurich and Lucerne, making it a vibrant nexus for business. The region’s excellent transport connectivity, including proximity to Zurich International Airport, ensures seamless global operations. Modern office buildings and state-of-the-art coworking spaces cater perfectly to the needs of contemporary enterprises.
                                    - Zug stands out with its business-friendly environment characterized by low tax regimes and supportive economic policies. This favorable climate has attracted numerous global companies like Glencore and Siemens, underscoring its appeal to industry giants. The diverse business ecosystem here supports a myriad of industries from finance and tech to commodities trading and pharmaceuticals. This diversity not only propels innovation but also enriches networking opportunities across various sectors.
                                    - Moreover, Zug boasts access to an exceptionally skilled workforce that is both highly educated and multilingual—an invaluable asset for any growing company. Whether you’re launching a startup or relocating an established business, Zug provides all the resources you need for success in one dynamic locale. Embrace this unique opportunity; set up your next venture amid the inspiring backdrop of Canton of Zug today!."""
    expected_output = """  - Office space in Canton of Zug offers a strategic advantage for businesses looking to establish a presence in Switzerland.
                                    - Zug is home to numerous renowned companies, including Roche Diagnostics, Siemens Smart Infrastructure, and Glencore.
                                    - The canton boasts a diverse range of industries such as life sciences, high-tech, fintech, and commodity trading.
                                    - Its central location in Switzerland provides excellent connectivity, with major transport hubs like Zurich Airport and Zurich Hauptbahnhof nearby.
                                    - Zug's business-friendly environment is characterized by low taxes, a stable political climate, and a high quality of life, making it an attractive destination for both startups and established enterprises.
                                    - The region also offers a robust infrastructure, including innovation platforms and institutions like the Switzerland Innovation Park Central and the Central Switzerland University of Applied Sciences and Arts.
                                    - With a strong network of industry-specific associations and a reputation for efficiency and professionalism, Zug is well-equipped to support business growth and innovation.
                                    - Whether you're in life sciences, high-tech, or finance, office space in Canton of Zug provides the ideal setting for your business to thrive."""
    retrieval_context = ["address: Neugasse 25, 6300 Zug, Switzerland", "population: 30000"] 
                               
    
    # Test case
    test_case = LLMTestCase(
        input=input,
        actual_output=actual_output,
        expected_output=expected_output,
        retrieval_context=retrieval_context
    )

    # EvalRAG metrics
    contextual_precision = ContextualPrecisionMetric(threshold=0.7, model="gpt-4o-mini", include_reason=True)
    contextual_recall = ContextualRecallMetric(threshold=0.7, model="gpt-4o-mini", include_reason=True)
    contextual_relevancy = ContextualRelevancyMetric(threshold=0.7, model="gpt-4o-mini", include_reason=True)
    answer_relevancy = AnswerRelevancyMetric(threshold=0.7, model="gpt-4o-mini", include_reason=True)
    faithfulness = FaithfulnessMetric(threshold=0.7, model="gpt-4o-mini", include_reason=True)

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
                print("Result: ", self.contextual_precision.success)
                print("Score: ", self.contextual_precision.score)
                print("Reason: ", self.contextual_precision.reason)
            case "recall":
                # Measure (--generation recall : evaluates only contextual recall metric)
                self.contextual_recall.measure(self.test_case)
                print("Result: ", self.contextual_recall.success)
                print("Score: ", self.contextual_recall.score)
                print("Reason: ", self.contextual_recall.reason)
            case "relevancy":
                # Measure (--generation relevancy : evaluates only contextual relevancy metric)
                self.contextual_relevancy.measure(self.test_case)
                print("Result: ", self.contextual_relevancy.success)
                print("Score: ", self.contextual_relevancy.score)
                print("Reason: ", self.contextual_relevancy.reason)
            case "answer":
                # Measure (--generation answer : evaluates only answer relevancy metric)
                self.answer_relevancy.measure(self.test_case)
                print("Result: ", self.answer_relevancy.success)
                print("Score: ", self.answer_relevancy.score)
                print("Reason: ", self.answer_relevancy.reason)
            case "faithfulness":
                # Measure (--generation faithfulness : evaluates only faithfulness metric)
                self.faithfulness.measure(self.test_case)
                print("Result: ", self.faithfulness.success)
                print("Score: ", self.faithfulness.score)
                print("Reason: ", self.faithfulness.reason)
