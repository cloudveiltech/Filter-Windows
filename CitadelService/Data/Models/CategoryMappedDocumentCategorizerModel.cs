using opennlp.tools.doccat;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace CitadelService.Data.Models
{
    internal class CategoryMappedDocumentCategorizerModel
    {
        public class ClassificationResult
        {
            public string BestCategoryName
            {
                get;
                private set;
            }

            public double BestCategoryScore
            {
                get;
                private set;
            }

            public ClassificationResult(string bestCategoryName, double bestCategoryScore)
            {
                BestCategoryName = bestCategoryName;
                BestCategoryScore = bestCategoryScore;
            }
        }

        private DocumentCategorizerME Categorizer
        {
            get;
            set;
        }

        private Dictionary<string, string> MappedCategories
        {
            get;
            set;
        }

        public CategoryMappedDocumentCategorizerModel(DocumentCategorizerME categorizer, Dictionary<string, string> mappedCategories)
        {
            Categorizer = categorizer;
            MappedCategories = mappedCategories;

            Debug.Assert(Categorizer != null, "Categorizer cannot be null!");

            Debug.Assert(MappedCategories != null, "Mapped categories cannot be null!");

            Debug.Assert(MappedCategories.Count > 0, "Mapped categories must contain one or more entries!");
            
            if(Categorizer == null)
            {
                throw new ArgumentException(nameof(categorizer), "Categorizer cannot be null!");
            }

            if(MappedCategories == null)
            {
                throw new ArgumentException(nameof(mappedCategories), "Mapped categories cannot be null!");
            }

            if(MappedCategories.Count <= 0)
            {
                throw new ArgumentException(nameof(categorizer), "Mapped categories must contain one or more entries!");
            }

            var totalInternalCats = categorizer.getNumberOfCategories();
            Debug.Assert(totalInternalCats == MappedCategories.Count, "Mapped categories have the same number of entries as it's corresponding categorizer!");

            if(totalInternalCats != MappedCategories.Count)
            {
                var whichArgName = totalInternalCats < MappedCategories.Count ? nameof(totalInternalCats) : nameof(mappedCategories);
                throw new ArgumentException(nameof(MappedCategories), "Mapped categories have the same number of entries as it's corresponding categorizer!");
            }

            for(int i = 0; i < totalInternalCats; ++i)
            {
                var internalCatName = Categorizer.getCategory(i);
                Debug.Assert(MappedCategories.ContainsKey(internalCatName) == true, "Found unmapped category!");

                if(MappedCategories.ContainsKey(internalCatName) == false)
                {
                    throw new ArgumentException(nameof(mappedCategories), "Found unmapped category!");
                }
            }
        }

        public ClassificationResult ClassifyText(string textToClassify)
        {
            // XXX TODO - OpenNLP people deprecated the method that takes a plain string. Is splitting here correct?
            // It seems to be, because not splitting gives us all categories with the same result basically (evenly split probabilities every time).
            var classResult = Categorizer.categorize(textToClassify.Split(' '));           
            var internalBestCat = Categorizer.getBestCategory(classResult);

            return new ClassificationResult(MappedCategories[internalBestCat], classResult.Max());
        }
    }
}
