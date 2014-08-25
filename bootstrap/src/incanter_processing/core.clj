(ns incanter-processing.core
  (:gen-class)
  (:require [clojure.string :as string]
    [clojure.contrib.io :as clio]
    [clojure.contrib.generic.math-functions :as gmath]
    [clojure.tools.cli :refer [parse-opts]]
    [clojure.pprint :as pprint]
    [clojure.java.io :as jio]
    [somnium.congomongo :refer :all]
    [gloss.core]
    [gloss.io]
    [incanter.core :refer :all] 
    [incanter.stats :refer :all] 
    [incanter.charts :refer :all]
    [incanter.io :refer :all]
    [incanter.datasets :refer :all]
    [incanter.mongodb :refer :all]
    )
  )

(defmacro get-version []
  (System/getProperty "incanter-processing.version"))

(defn print-info!
  "Print message depending on verbosity level"
  [message message-level verbosity-level]
  (when (>= verbosity-level message-level) (println message)))

(defn pretty-str
  "Returns a nice string representation of the object"
  [object]
  (with-out-str (pprint/write object))
  )

(defn sum-of-sq
  "Returns the sum of squares of vector components"
  [v]
  (reduce + (mapv * v v)))

(defn- find-bsearch-interval
  "Return an interval containing an x such that (= (function x) target) is true.
  The supplied function should be a monotonous function of x."
  [function target init-start init-end]
  (let [increasing? (< (function init-start) (function init-end))
        decrease-start? (if increasing?
                          (fn [x] (> (function x) target)) 
                          (fn [x] (< (function x) target)))
        increase-end? (complement decrease-start?)]
    (loop [start init-start
           end init-end] 
      (if (and 
            (not (decrease-start? start)) 
            (not (increase-end? end)))
        [start end] 
        (let [new-start (if (decrease-start? start) (- start (- end start)) start)
              new-end (if (increase-end? end) (+ end (- end start)) end)
              ] (recur new-start new-end))))))

(defn bsearch-invert
  "Returns the argument of a one-dimensional decreasing function for the supplied value"
  [function target epsilon init-start init-end]
  (let [increasing? (< (function init-start) (function init-end))
        increase-start? (if increasing?
                          (fn [x] (< (function x) target)) 
                          (fn [x] (> (function x) target)))
        decrease-end? (complement increase-start?) 
        [loop-init-start loop-init-end] (find-bsearch-interval function target 0.0 1.0)]
    (loop [start loop-init-start 
           end loop-init-end]
      (let [x (* 0.5 (+ start end))] 
        (if (< (gmath/abs (- target (function x))) epsilon) 
          x
          (recur
            (if (increase-start? x) x start)
            (if (decrease-end? x) x end)))))))

(defn read-binary-float64-le
  "Returns a vector of doubles from a little endian binary file"
  [filename]
  (let [fr (gloss.core/compile-frame (gloss.core/repeated :float64-le :prefix :none))
        raw-data (clio/to-byte-array (clio/as-file filename))]
    (gloss.io/decode fr raw-data)))

(defn- compute-magnitude
  "Return the NMR signal magnitude for a set of phase values.
  Multiply phase values by scale before computing magnitude."
  [phase scale]
  (let [scaled-phase (map (partial * scale) phase)
        s-real (apply + (map gmath/cos scaled-phase))
        s-imag (apply + (map gmath/sin scaled-phase))]
    (gmath/sqrt (sum-of-sq [s-real s-imag]))))

(defn- get-gxg-linb
  "Return a collection of n values for the product of the gamma and the 
  gradient so that b-values are linearly distributed and max b is 
  optimal."
  [phase n]
  (let [function (partial compute-magnitude phase)
        target (* (gmath/exp -1.0) (function 0.0))
        epsilon 1.0e-3
        init-start 0.0
        init-end 1.0
        gxg-max (bsearch-invert function target epsilon init-start init-end)
        gxg-max2 (* gxg-max gxg-max)
        delta-gxg2 (/ gxg-max2 (- n 1))]
    (mapv gmath/sqrt (range 0.0 (+ gxg-max2 (* 0.5 delta-gxg2)) delta-gxg2))))

(defn- compute-adc-m
  "Return a map with the values for one shape."
  [phase b-gxg-one gxg-linb b-values]
  (let [magnitudes (mapv (partial compute-magnitude phase) gxg-linb)
        log-m (mapv gmath/log magnitudes)
        linear-model (linear-model log-m b-values)
        adc-m (* -1.0 (second (linear-model :coefs)))]
    adc-m))

(defn- compute-adc-phase
  "Return a map with the values for one shape."
  [phase b-gxg-one]
  (let [mean-phase2 (/ (sum-of-sq phase) (count phase))
        adc-phase (/ mean-phase2 (* 2.0 b-gxg-one))]
    adc-phase))

(defn my-kurt 
  "Returns sample kurtosis"
  [x] 
  (let [m (mean x) 
        v (variance x) 
        s4 (apply + (pow (map #(- % m) x) 4))
        N-1 (dec  (count x))] 
    (- (/ s4 (* v v N-1)) 3.0)))

(defn bootstrap-case 
  ""
  [data func bsize]
  (let [b (bootstrap data func :size bsize)
        f-orig (func data)
        f-mean (mean b)
        f-sd (sd b)
        f-bias (- f-mean f-orig)
        f-ci95 (quantile b :probs [0.025 0.975])
        f-ci99 (quantile b :probs [0.005 0.995])
        ]
    {;:bootstrap b
     :orig f-orig
     :mean f-mean
     :se f-sd
     :bias f-bias
     :ci95 f-ci95
     :ci99 f-ci99
     }))

(defn -main
  "I don't do a whole lot ... yet."
  [& args]
  (let [prog-info {:name "Diffusion MR phase and ADC calculator"
                   :description "Thinking about a name..."
                   :version (get-version)}

        jre-info {:name (System/getProperty "java.runtime.name")
                  :vendor (System/getProperty "java.vendor")
                  :version (System/getProperty "java.runtime.version")}

        jvm-info {:name (System/getProperty "java.vm.name")
                  :version (System/getProperty "java.vm.version")
                  :info (System/getProperty "java.vm.info")}

        os-info {:name (System/getProperty "os.name")
                 :version (System/getProperty "os.version")
                 :arch (System/getProperty "os.arch")}

        version-str (string/join "" 
                                 [(println-str (format "This is the Clojure implementation of %s version %s" (prog-info :name) (prog-info :version)))
                                  (println-str "(For help use the -h or --help command line option)")
                                  (println-str (format "JRE: %s %s on %s %s (%s)" (jre-info :name) (jre-info :version) (os-info :name) (os-info :version) (os-info :arch)))
                                  (println-str (format "JVM: %s (build %s %s)" (jvm-info :name) (jvm-info :version) (jvm-info :info)))])


        cli-options-spec [["-h" "--help" "Show help and version info" :flag true :default false] 
                          ["-v" "--verbosity NUMBER" "Verbosity level (0->Quiet, 1->Normal, 2->Verbose)" :default 1 :parse-fn (comp int read-string)] 
                          ["-f" "--file PATH" "File with little endian binary phase data"]
                          ["-b" "--b-values NUMBER" "Number of b-values for ADC fit" :default 10 :parse-fn (comp int read-string)]
                          ["-s" "--bootstrap-size NUMBER" "Bootstrap size" :default 1000 :parse-fn (comp int read-string)]
                  ]

        ;; Get command line arguments
        parse-opts-results (parse-opts args cli-options-spec)

        cli-opts (:options parse-opts-results)
        banner (:summary parse-opts-results)
        usage "Usage with jar file: java -jar <jarfile> [options]\nUsage with Leiningen: lein run -m incanter-processing.core [options]"

        cli-help (string/join "" [usage "\n\n" "Options:" "\n" banner])

        ;; Print help and exit when help switch is passed
        _ (when (cli-opts :help) (println version-str) (println cli-help) (System/exit 0))

        verb-level (cli-opts :verbosity)
        filename (:file cli-opts)
        nb (:b-values cli-opts)
        bootstrap-size (:bootstrap-size cli-opts)

        ;; Print program and Java version info
        ;_ (when (cli-opts :verbose) (print-info! version-str))
        _ (print-info! version-str 1 verb-level)

        sqrtb-coeff {:nmr-phase-delta 1.0 :nmr-phase-square 3.46410161513775458705 :nmr-phase-sin 5.13019932064745638218}
        phase-all (partition 3 (read-binary-float64-le filename))

        _ (print-info! (format "Reading phase values from \"%s\"" filename) 1 verb-level)
        _ (print-info! (format "A total of %d phase values read" (* 3 (count phase-all))) 1 verb-level)
        _ (print-info! (format "Bootstrapping size is %d" bootstrap-size) 1 verb-level)
        
        phase-delta (mapv first phase-all)
        phase-square (mapv second phase-all)
        phase-sin (mapv last phase-all)

        sqrtb-coeff-delta (:nmr-phase-delta sqrtb-coeff)
        b-gxg-one-delta (/ 1.0 (* sqrtb-coeff-delta sqrtb-coeff-delta))
        gxg-linb-delta (get-gxg-linb (vec phase-delta) nb)
        b-values-delta (mapv (fn [x] (* (* x x) b-gxg-one-delta) ) gxg-linb-delta)

        sqrtb-coeff-square (:nmr-phase-square sqrtb-coeff)
        b-gxg-one-square (/ 1.0 (* sqrtb-coeff-square sqrtb-coeff-square))
        gxg-linb-square (get-gxg-linb (vec phase-square) nb)
        b-values-square (mapv (fn [x] (* (* x x) b-gxg-one-square) ) gxg-linb-square)

        sqrtb-coeff-sin (:nmr-phase-sin sqrtb-coeff)
        b-gxg-one-sin (/ 1.0 (* sqrtb-coeff-sin sqrtb-coeff-sin))
        gxg-linb-sin (get-gxg-linb (vec phase-sin) nb)
        b-values-sin (mapv (fn [x] (* (* x x) b-gxg-one-sin) ) gxg-linb-sin)

        todo {
              :phase-delta-mean (partial bootstrap-case phase-delta mean bootstrap-size)
              :phase-square-mean (partial bootstrap-case phase-square mean bootstrap-size)
              :phase-sin-mean (partial bootstrap-case phase-sin mean bootstrap-size)

              :phase-delta-skewness (partial bootstrap-case phase-delta skewness bootstrap-size)
              :phase-square-skewness (partial bootstrap-case phase-square skewness bootstrap-size)
              :phase-sin-skewness (partial bootstrap-case phase-sin skewness bootstrap-size)

              :phase-delta-kurtosis (partial bootstrap-case phase-delta my-kurt bootstrap-size)
              :phase-square-kurtosis (partial bootstrap-case phase-square my-kurt bootstrap-size)
              :phase-sin-kurtosis (partial bootstrap-case phase-sin my-kurt bootstrap-size)

              :phase-delta-adc-p (partial bootstrap-case phase-delta (fn [d] (compute-adc-phase d b-gxg-one-delta)) bootstrap-size)
              :phase-square-adc-p (partial bootstrap-case phase-square (fn [d] (compute-adc-phase d b-gxg-one-square)) bootstrap-size)
              :phase-sin-adc-p (partial bootstrap-case phase-sin (fn [d] (compute-adc-phase d b-gxg-one-sin)) bootstrap-size)

              :phase-delta-adc-m (partial bootstrap-case phase-delta (fn [d] (compute-adc-m d b-gxg-one-delta gxg-linb-delta b-values-delta)) bootstrap-size)
              :phase-square-adc-m (partial bootstrap-case phase-square (fn [d] (compute-adc-m d b-gxg-one-square gxg-linb-square b-values-square)) bootstrap-size)
              :phase-sin-adc-m (partial bootstrap-case phase-sin (fn [d] (compute-adc-m d b-gxg-one-sin gxg-linb-sin b-values-sin)) bootstrap-size)
              }

        case-keys (keys todo)

        case-results (map (fn [k] (print-info! (format "Bootstraping %s" (name k)) 1 verb-level) ((k todo))) case-keys)

        results (zipmap case-keys case-results)

        _ (print-info! (pretty-str results) 1 verb-level)
        ]
        (shutdown-agents) 
    ))
